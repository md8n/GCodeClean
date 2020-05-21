// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace GCodeClean.Processing
{
    public static class Processing
    {
        public static async IAsyncEnumerable<Line> Clip(this IAsyncEnumerable<Line> tokenizedLines)
        {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            await foreach (var line in tokenizedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                for (var ix = 0; ix < line.Tokens.Count; ix++)
                {
                    if (!line.Tokens[ix].IsValid)
                    {
                        continue;
                    }

                    var replacement = (JObject)replacements[line.Tokens[ix].Source];
                    if (replacement != null)
                    {
                        foreach (var contextToken in replacement)
                        {
                            context[contextToken.Key] = (string)contextToken.Value;
                        }
                    }

                    var wholeCode = (string)tokenDefs[line.Tokens[ix].Source];
                    if (wholeCode != null)
                    {
                        continue;
                    }
                    var subToken = "" + line.Tokens[ix].Code;
                    var subCode = (string)tokenDefs[subToken];
                    if (subCode != null)
                    {
                        decimal? value = line.Tokens[ix].Number;
                        var hasUnits = context.ContainsKey("lengthUnits");
                        if (hasUnits && value.HasValue)
                        {
                            // Round to 3dp for mm and 4dp for inch
                            var clip = (context["lengthUnits"] == "mm") ? 3 : 4;
                            var clipFormat = clip == 1 ? "{0}{1:0.###}" : "{0}{1:0.####}";
                            value = Math.Round(value.Value, clip);
                            line.Tokens[ix].Source = String.Format(clipFormat, subToken, value);
                        }
                    }
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Augment(this IAsyncEnumerable<Line> tokenizedLines)
        {
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token>() { new Token("X"), new Token("Y"), new Token("Z") };
            var previousIJKCoords = new List<Token>() { new Token("I"), new Token("J") };

            await foreach (var line in tokenizedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var hasXY = line.HasTokens(new List<char> { 'X', 'Y' });
                var hasZ = line.HasTokens(new List<char> { 'Z' });
                var hasIJ = line.HasTokens(new List<char> { 'I', 'J' });
                var hasK = line.HasTokens(new List<char> { 'K' });
                if (hasK)
                {
                    previousIJKCoords.Add(new Token("K"));
                }

                if (hasXY || hasZ || hasIJ || hasK)
                {
                    if (line.HasMovementCommand())
                    {
                        foreach (var token in line.Tokens)
                        {
                            if (Token.MovementCommands.Contains(token.Source))
                            {
                                previousCommand = token;
                                break;
                            }
                        }
                    }
                    else if (previousCommand.IsCommand)
                    {
                        line.Tokens.Insert(0, previousCommand);
                    }
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    var newCoord = line.Tokens.FirstOrDefault(t => t.Code == previousXYZCoords[ix].Code);
                    if (newCoord is null)
                    {
                        newCoord = previousXYZCoords[ix];
                    }
                    else
                    {
                        previousXYZCoords[ix] = newCoord;
                    }
                }

                for (var ix = 0; ix < previousIJKCoords.Count; ix++)
                {
                    var newCoord = line.Tokens.FirstOrDefault(t => t.Code == previousIJKCoords[ix].Code);
                    if (newCoord is null)
                    {
                        newCoord = previousIJKCoords[ix];
                    }
                    else
                    {
                        previousIJKCoords[ix] = newCoord;
                    }
                }

                // Remove and then add back in the arguments - ensures consistency
                if (hasXY || hasZ)
                {
                    line.RemoveTokens(new List<char> { 'X', 'Y', 'Z' });
                    line.Tokens.AddRange(previousXYZCoords.Where(pc => pc.IsArgument));
                }

                if (hasIJ || hasK)
                {
                    line.RemoveTokens(new List<char> { 'I', 'J', 'K' });
                    line.Tokens.AddRange(previousIJKCoords.Where(pc => pc.IsArgument));
                }

                yield return line;

                // Keep this for later but it requires understanding plane selection
                // if (hasZ) {
                //     var maybeZ = tokens.First(t => t[0] == 'Z').ExtractCoord();
                //     if (maybeZ.HasValue && maybeZ.Value > 0) {
                //         // throw in a blank line after the Z lift
                //         yield return new List<string>();
                //     }
                // }
            }
        }

        public static async IAsyncEnumerable<Line> ConvertArcRadiusToCenter(this IAsyncEnumerable<Line> tokenizedLines)
        {
            var previousCoords = new Coord();

            var clockwiseMovementToken = new Token("G2");

            await foreach (var line in tokenizedLines)
            {
                var hasMovement = line.HasMovementCommand();
                if (!hasMovement)
                {
                    yield return line;
                    continue;
                }

                Coord coords = line;
                if (!previousCoords.HasCoordPair())
                {
                    // Some movement command, and we're at a 'start'
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return line;
                    continue;
                }

                var radius = line.Tokens.FirstOrDefault(t => t.Code == 'R')?.Number;
                if (!radius.HasValue || !coords.HasCoordPair())
                {
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return line;
                    continue;
                }

                var intersections = Utility.FindIntersections(coords, previousCoords, radius.Value);
                if (intersections.Count == 0)
                {
                    // malformed Arc Radius
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return line;
                    continue;
                }

                if (intersections.Count == 2)
                {
                    var isClockwise = line.Tokens.Contains(clockwiseMovementToken);
                    var isClockwiseIntersection = Utility.DirectionOfPoint(previousCoords.ToPointF(), coords.ToPointF(), intersections[0].ToPointF()) < 0;

                    intersections.RemoveAt((isClockwise != isClockwiseIntersection) ? 0 : 1);
                }

                previousCoords = Coord.Merge(previousCoords, coords, true);
                yield return line.ArcRadiusToCenter(previousCoords, intersections[0]);
            }
        }

        private static Line ArcRadiusToCenter(this Line lineB, Coord coordsA, Coord center) {
            lineB.RemoveTokens(new List<char>() { 'R'});

            if ((center.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X)
            {
                lineB.Tokens.Add(new Token($"I{center.X - coordsA.X:0.####}"));
            }
            if ((center.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y)
            {
                lineB.Tokens.Add(new Token($"J{center.Y - coordsA.Y:0.####}"));
            }
            if ((center.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z)
            {
                lineB.Tokens.Add(new Token($"K{center.Z - coordsA.Z:0.####}"));
            }

            return lineB;
        }

        public static async IAsyncEnumerable<Line> Annotate(this IAsyncEnumerable<Line> tokenizedLines)
        {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            var previousTokenCodes = new List<string>();

            await foreach (var line in tokenizedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var annotationTokens = new List<string>();
                var tokenCodes = new List<string>();
                foreach (var token in line.Tokens)
                {
                    var replacement = (JObject)replacements[token];
                    if (replacement != null)
                    {
                        foreach (var contextToken in replacement)
                        {
                            context[contextToken.Key] = (string)contextToken.Value;
                        }
                    }

                    var annotation = (string)tokenDefs[token];
                    if (annotation is null && token.Number.HasValue)
                    {
                        var subToken = "" + token.Code;
                        tokenCodes.Add(subToken);
                        annotation = (string)tokenDefs[subToken];
                        context[token.Code + "value"] = token.Number.Value.ToString();
                    }
                    else
                    {
                        tokenCodes.Add(token.ToString());
                    }
                    if (annotation != null)
                    {
                        foreach (var contextToken in context)
                        {
                            annotation = annotation.Replace("{" + contextToken.Key + "}", contextToken.Value);
                        }
                        annotationTokens.Add(annotation);
                    }
                }
                var isDuplicate = true;
                if (previousTokenCodes.Count != tokenCodes.Count)
                {
                    isDuplicate = false;
                }
                else
                {
                    for (var ix = 0; ix < tokenCodes.Count; ix++)
                    {
                        if (previousTokenCodes[ix] != tokenCodes[ix])
                        {
                            isDuplicate = false;
                            break;
                        }
                    }
                }

                if (!isDuplicate && annotationTokens.Count > 0)
                {
                    line.Tokens.Add(new Token($"({string.Join(", ", annotationTokens)})"));
                    previousTokenCodes = tokenCodes;
                }

                yield return line;
            }
        }
    }
}
