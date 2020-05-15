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
        public static async IAsyncEnumerable<List<string>> Clip(this IAsyncEnumerable<List<string>> tokenizedLines)
        {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            await foreach (var tokens in tokenizedLines)
            {
                if (tokens.IsNotCommandOrArguments())
                {
                    yield return tokens;
                    continue;
                }

                for (var ix = 0; ix < tokens.Count; ix++)
                {
                    var replacement = (JObject)replacements[tokens[ix]];
                    if (replacement != null)
                    {
                        foreach (var contextToken in replacement)
                        {
                            context[contextToken.Key] = (string)contextToken.Value;
                        }
                    }

                    var wholeCode = (string)tokenDefs[tokens[ix]];
                    if (wholeCode != null)
                    {
                        continue;
                    }
                    var subToken = "" + tokens[ix][0];
                    var subCode = (string)tokenDefs[subToken];
                    if (subCode != null)
                    {
                        decimal? value = tokens[ix].ExtractCoord();
                        var hasUnits = context.ContainsKey("lengthUnits");
                        var hasDP = tokens[ix].IndexOf(".") != -1;
                        if (hasUnits && hasDP && value.HasValue)
                        {
                            // Round to 3dp for mm and 4dp for inch
                            var clip = (context["lengthUnits"] == "mm") ? 3 : 4;
                            var clipFormat = clip == 1 ? "{0}{1:0.###}" : "{0}{1:0.####}";
                            value = Math.Round(value.Value, clip);
                            tokens[ix] = String.Format(clipFormat, subToken, value);
                        }
                    }
                }

                yield return tokens;
            }
        }

        public static async IAsyncEnumerable<List<string>> Augment(this IAsyncEnumerable<List<string>> tokenizedLines)
        {
            var previousCommand = "";
            var previousXYZCoords = new List<string>() { "X", "Y", "Z" };
            var previousIJKCoords = new List<string>() { "I", "J" };

            await foreach (var tokens in tokenizedLines)
            {
                if (tokens.IsNotCommandOrArguments())
                {
                    yield return tokens;
                    continue;
                }

                var hasXY = tokens.Any(t => new[] { 'X', 'Y' }.Contains(t[0]));
                var hasZ = tokens.Any(t => t[0] == 'Z');
                var hasIJ = tokens.Any(t => new[] { 'I', 'J' }.Contains(t[0]));
                var hasK = tokens.Any(t => t[0] == 'K');
                if (hasK)
                {
                    previousIJKCoords.Add("K");
                }

                if (hasXY || hasZ || hasIJ || hasK)
                {
                    if (tokens.HasMovementCommand())
                    {
                        foreach (var token in tokens)
                        {
                            if (Utility.MovementCommands.Contains(token))
                            {
                                previousCommand = token;
                                break;
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(previousCommand))
                    {
                        tokens.Insert(0, previousCommand);
                    }
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousXYZCoords[ix][0]);
                    if (newCoord == null)
                    {
                        newCoord = previousXYZCoords[ix];
                    }
                    previousXYZCoords[ix] = newCoord;
                }

                for (var ix = 0; ix < previousIJKCoords.Count; ix++)
                {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousIJKCoords[ix][0]);
                    if (newCoord == null)
                    {
                        newCoord = previousIJKCoords[ix];
                    }
                    previousIJKCoords[ix] = newCoord;
                }

                if (hasXY || hasZ)
                {
                    for (var ix = tokens.Count - 1; ix >= 0; ix--)
                    {
                        if (tokens[ix][0] == 'X' || tokens[ix][0] == 'Y' || tokens[ix][0] == 'Z')
                        {
                            tokens.RemoveAt(ix);
                        }
                    }
                    tokens.AddRange(previousXYZCoords.Where(pc => pc.Length > 1));
                }

                if (hasIJ || hasK)
                {
                    for (var ix = tokens.Count - 1; ix >= 0; ix--)
                    {
                        if (tokens[ix][0] == 'I' || tokens[ix][0] == 'J' || tokens[ix][0] == 'K')
                        {
                            tokens.RemoveAt(ix);
                        }
                    }
                    tokens.AddRange(previousIJKCoords.Where(pc => pc.Length > 1));
                }

                yield return tokens;

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

        public static async IAsyncEnumerable<List<string>> ConvertArcRadiusToCenter(this IAsyncEnumerable<List<string>> tokenizedLines)
        {
            var previousCoords = new List<string>() { "X", "Y", "Z" }.ExtractCoords();

            await foreach (var tokens in tokenizedLines)
            {
                var hasMovement = tokens.HasMovementCommand();
                if (!hasMovement)
                {
                    yield return tokens;
                    continue;
                }

                var coords = tokens.ExtractCoords();
                if (!previousCoords.HasCoordPair())
                {
                    // Some movement command, and we're at a 'start'
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return tokens;
                    continue;
                }

                var radius = tokens.FirstOrDefault(t => t[0] == 'R').ExtractCoord();
                if (!radius.HasValue || !coords.HasCoordPair())
                {
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return tokens;
                    continue;
                }

                var intersections = Utility.FindIntersections(coords, previousCoords, radius.Value);
                if (intersections.Count == 0)
                {
                    // malformed Arc Radius
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return tokens;
                    continue;
                }

                if (intersections.Count == 2)
                {
                    var isClockwise = tokens.Any(t => t == "G2" || t == "G02");
                    var isClockwiseIntersection = Utility.DirectionOfPoint(previousCoords.ToPointF(), coords.ToPointF(), intersections[0].ToPointF()) < 0;

                    intersections.RemoveAt((isClockwise != isClockwiseIntersection) ? 0 : 1);
                }

                previousCoords = Coord.Merge(previousCoords, coords, true);
                yield return tokens.ArcRadiusToCenter(previousCoords, intersections[0]);
            }
        }

        private static List<string> ArcRadiusToCenter(this List<string> tokensB, Coord coordsA, Coord center) {
            tokensB = tokensB.Where(t => t[0] != 'R').ToList();

            if ((center.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X)
            {
                tokensB.Add($"I{center.X - coordsA.X:0.####}");
            }
            if ((center.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y)
            {
                tokensB.Add($"J{center.Y - coordsA.Y:0.####}");
            }
            if ((center.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z)
            {
                tokensB.Add($"K{center.Z - coordsA.Z:0.####}");
            }

            return tokensB;
        }

        public static async IAsyncEnumerable<List<string>> Annotate(this IAsyncEnumerable<List<string>> tokenizedLines)
        {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            var previousTokenCodes = new List<string>();

            await foreach (var tokens in tokenizedLines)
            {
                if (tokens.IsNotCommandOrArguments())
                {
                    yield return tokens;
                    continue;
                }

                var annotationTokens = new List<string>();
                var tokenCodes = new List<string>();
                foreach (var token in tokens)
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
                    if (annotation == null)
                    {
                        var subToken = "" + token[0];
                        tokenCodes.Add(subToken);
                        annotation = (string)tokenDefs[subToken];
                        context[token[0] + "value"] = (string)token.Substring(1);
                    }
                    else
                    {
                        tokenCodes.Add(token);
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
                    tokens.Add($"({string.Join(", ", annotationTokens)})");
                    previousTokenCodes = tokenCodes;
                }

                yield return tokens;
            }
        }
    }
}
