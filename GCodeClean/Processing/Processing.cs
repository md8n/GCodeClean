// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Newtonsoft.Json.Linq;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Processing
    {
        public static async IAsyncEnumerable<Line> InjectPreamble(this IAsyncEnumerable<Line> tokenisedLines,
            Context preamble)
        {
            var preambleOutput = false;
            await foreach (var line in tokenisedLines) {
                if (line.HasTokens(preamble.ModalMotion))
                {
                    var linesToOutput = preamble.NonOutputLines();
                    if (linesToOutput.Count > 0)
                    {
                        linesToOutput.Insert(0, new Line("(Preamble completed by GCodeClean)"));
                        linesToOutput.Add(new Line("(Preamble completed by GCodeClean)"));
                        foreach (var preambleLine in linesToOutput)
                        {
                            yield return preambleLine;
                        }
                    }
                    preamble.FlagAllLinesAsOutput();
                    preambleOutput = true;
                }

                if (!preambleOutput)
                {
                    preamble.Update(line, true);
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Clip(this IAsyncEnumerable<Line> tokenisedLines, JObject tokenDefinitions)
        {
            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            await foreach (var line in tokenisedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                foreach (var token in line.Tokens)
                {
                    if (!token.IsValid)
                    {
                        continue;
                    }

                    var replacement = (JObject)replacements[token.Source];
                    if (replacement != null)
                    {
                        foreach (var (ctkey, ctvalue) in replacement)
                        {
                            context[ctkey] = (string)ctvalue;
                        }
                    }

                    var wholeCode = (string)tokenDefs[token.Source];
                    if (wholeCode != null)
                    {
                        continue;
                    }
                    var subToken = "" + token.Code;
                    var subCode = (string)tokenDefs[subToken];
                    if (subCode == null)
                    {
                        continue;
                    }

                    var value = token.Number;
                    var hasUnits = context.ContainsKey("lengthUnits");
                    if (!(hasUnits && value.HasValue))
                    {
                        continue;
                    }

                    // Round to 3dp for mm and 4dp for inch
                    var clip = context["lengthUnits"] == "mm" ? 3 : 4;
                    var clipFormat = clip == 1 ? "{0}{1:0.###}" : "{0}{1:0.####}";
                    value = Math.Round(value.Value, clip);
                    token.Source = string.Format(clipFormat, subToken, value);
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Augment(this IAsyncEnumerable<Line> tokenisedLines)
        {
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token> { new Token("X"), new Token("Y"), new Token("Z") };
            var previousIJKCoords = new List<Token> { new Token("I"), new Token("J") };

            await foreach (var line in tokenisedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var hasXY = line.HasTokens(new List<char> { 'X', 'Y' });
                var hasZ = line.HasToken('Z');
                var hasIJ = line.HasTokens(new List<char> { 'I', 'J' });
                var hasK = line.HasToken('K');
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
                            if (!Token.MovementCommands.Contains(token.Source))
                            {
                                continue;
                            }

                            previousCommand = token;
                            break;
                        }
                    }
                    else if (previousCommand.IsCommand)
                    {
                        line.Tokens.Insert(0, previousCommand);
                    }
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    previousXYZCoords[ix] = line.Tokens.FirstOrDefault(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                }

                for (var ix = 0; ix < previousIJKCoords.Count; ix++)
                {
                    previousIJKCoords[ix] = line.Tokens.FirstOrDefault(t => t.Code == previousIJKCoords[ix].Code) ?? previousIJKCoords[ix];
                }

                // Remove and then add back in the arguments - ensures consistency
                if (hasXY || hasZ)
                {
                    line.RemoveTokens(new List<char> { 'X', 'Y', 'Z' });
                    line.Tokens.AddRange(previousXYZCoords.Where(pc => pc.IsArgument && pc.IsValid));
                }

                if (hasIJ || hasK)
                {
                    line.RemoveTokens(new List<char> { 'I', 'J', 'K' });
                    line.Tokens.AddRange(previousIJKCoords.Where(pc => pc.IsArgument && pc.IsValid));
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

        public static async IAsyncEnumerable<Line> ConvertArcRadiusToCenter(this IAsyncEnumerable<Line> tokenisedLines)
        {
            var previousCoords = new Coord();

            var clockwiseMovementToken = new Token("G2");

            await foreach (var line in tokenisedLines)
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
                switch (intersections.Count)
                {
                    case 0:
                        // malformed Arc Radius
                        previousCoords = Coord.Merge(previousCoords, coords, true);

                        yield return line;
                        continue;
                    case 2:
                    {
                        var isClockwise = line.Tokens.Contains(clockwiseMovementToken);
                        var isClockwiseIntersection = Utility.DirectionOfPoint(previousCoords.ToPointF(), coords.ToPointF(), intersections[0].ToPointF()) < 0;

                        intersections.RemoveAt(isClockwise != isClockwiseIntersection ? 0 : 1);
                        break;
                    }
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

        public static async IAsyncEnumerable<Line> Annotate(this IAsyncEnumerable<Line> tokenisedLines, JObject tokenDefinitions)
        {
            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            var previousTokenCodes = new List<string>();

            await foreach (var line in tokenisedLines)
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
                    var replacement = (JObject)replacements[token.ToString()];
                    if (replacement != null)
                    {
                        foreach (var (key, value) in replacement)
                        {
                            context[key] = (string)value;
                        }
                    }

                    var annotation = (string)tokenDefs[token.ToString()];
                    if (annotation is null && token.Number.HasValue)
                    {
                        var subToken = "" + token.Code;
                        tokenCodes.Add(subToken);
                        annotation = (string)tokenDefs[subToken];
                        context[token.Code + "value"] = token.Number.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        tokenCodes.Add(token.ToString());
                    }

                    if (annotation == null)
                    {
                        continue;
                    }

                    foreach (var (key, value) in context)
                    {
                        annotation = annotation.Replace("{" + key + "}", value);
                    }
                    annotationTokens.Add(annotation);
                }
                var isDuplicate = true;
                if (previousTokenCodes.Count != tokenCodes.Count)
                {
                    isDuplicate = false;
                }
                else
                {
                    if (tokenCodes.Where((t, ix) => previousTokenCodes[ix] != t).Any())
                    {
                        isDuplicate = false;
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
