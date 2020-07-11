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
                if (line.HasTokens(ModalGroup.ModalAllMotion))
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
        
        public static async IAsyncEnumerable<Line> Augment(this IAsyncEnumerable<Line> tokenisedLines)
        {
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token> { new Token("X"), new Token("Y"), new Token("Z") };

            await foreach (var line in tokenisedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var hasXY = line.HasTokens(new List<char> { 'X', 'Y' });
                var hasZ = line.HasToken('Z');

                if (hasXY || hasZ)
                {
                    if (line.HasMovementCommand())
                    {
                        foreach (var token in line.Tokens)
                        {
                            if (!ModalGroup.ModalSimpleMotion.Contains(token))
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

                // Remove and then add back in the arguments - ensures consistency
                if (hasXY || hasZ)
                {
                    line.RemoveTokens(new List<char> { 'X', 'Y', 'Z' });
                    line.Tokens.AddRange(previousXYZCoords.Where(pc => pc.IsArgument && pc.IsValid));

                    line.Tokens.AddRange(line.RemoveTokens(new List<char> { 'I' }));
                    line.Tokens.AddRange(line.RemoveTokens(new List<char> { 'J' }));
                    line.Tokens.AddRange(line.RemoveTokens(new List<char> { 'K' }));
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

        public static async IAsyncEnumerable<Line> ZClamp(this IAsyncEnumerable<Line> tokenisedLines, decimal zClamp = 10.0M)
        {
            var context = Default.Preamble();

            await foreach (var line in tokenisedLines)
            {
                context.Update(line, true);

                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }
                
                var unitsCommand = context.GetModalState(ModalGroup.ModalUnits);
                if (unitsCommand == null) {
                    yield return line;
                    continue;
                }
                var lengthUnits = unitsCommand.ToString() == "G20" ? "inch" : "mm";

                if (lengthUnits == "mm") {
                    if (zClamp < 0.5M) {
                        zClamp = 0.5M;
                    } else if (zClamp > 10.0M) {
                        zClamp = 10.0M;
                    }
                } else {
                    if (zClamp < 0.05M) {
                        zClamp = 0.05M;
                    } else if (zClamp > 0.5M) {
                        zClamp = 0.5M;
                    }                    
                }

                var hasZ = line.HasToken('Z');
                var hasTravelling = line.HasToken("G0");

                if (hasZ && hasTravelling)
                {
                    var zTokenIndex = line.Tokens.FindIndex(t => t.Code == 'Z');

                    if (line.Tokens[zTokenIndex].Number > 0) {
                        line.Tokens[zTokenIndex].Number = zClamp;
                    }
                }

                yield return line;
            }
        }

        /// <summary>
        /// Convert Arc movement commands from using R to using IJ
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> ConvertArcRadiusToCenter(this IAsyncEnumerable<Line> tokenisedLines)
        {
            var previousCoords = new Coord();
            var context = Default.Preamble();

            var clockwiseMovementToken = new Token("G2");

            await foreach (var line in tokenisedLines)
            {
                context.Update(line, true);
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

                var intersections = Utility.FindIntersections(coords, previousCoords, radius.Value, context);
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

        public static async IAsyncEnumerable<Line> SimplifyShortArcs(this IAsyncEnumerable<Line> tokenisedLines, decimal arcTolerance = 0.0005M)
        {
            var context = Default.Preamble();
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token> { new Token("X"), new Token("Y"), new Token("Z") };
            var arcArguments = new List<char> { 'I', 'J', 'K' };
            var arcCommands = new List<Token> {
                new Token("G2"), new Token("G3")
            };

            await foreach (var line in tokenisedLines)
            {
                context.Update(line, true);

                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }
                
                var unitsCommand = context.GetModalState(ModalGroup.ModalUnits);
                if (unitsCommand == null) {
                    yield return line;
                    continue;
                }
                var lengthUnits = unitsCommand.ToString() == "G20" ? "inch" : "mm";

                var hasXY = line.HasTokens(new List<char> { 'X', 'Y' });
                var hasZ = line.HasToken('Z');

                if (hasXY || hasZ)
                {
                    if (line.HasMovementCommand())
                    {
                        foreach (var token in line.Tokens)
                        {
                            if (!ModalGroup.ModalSimpleMotion.Contains(token))
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
                
                if (!line.HasTokens(arcCommands))
                {
                    for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                    {
                        previousXYZCoords[ix] = line.Tokens.FirstOrDefault(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                    }

                    yield return line;
                    continue;
                }

                Coord coordsA = new Line(previousXYZCoords);
                Coord coordsB = line;
                var abDistance = (coordsA, coordsB).Distance();
                if (abDistance <= arcTolerance) {
                    line.RemoveTokens(arcArguments);
                    line.RemoveTokens(arcCommands);
                    line.Tokens.Insert(0, new Token("G1"));
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    previousXYZCoords[ix] = line.Tokens.FirstOrDefault(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Clip(this IAsyncEnumerable<Line> tokenisedLines, decimal tolerance = 0.0005M)
        {
            var context = Default.Preamble();
            var arcArguments = new [] { 'I', 'J', 'K' };

            await foreach (var line in tokenisedLines)
            {
                context.Update(line, true);

                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var unitsCommand = context.GetModalState(ModalGroup.ModalUnits);
                if (unitsCommand == null) {
                    yield return line;
                    continue;
                }
                var lengthUnits = unitsCommand.ToString() == "G20" ? "inch" : "mm";

                foreach (var token in line.Tokens)
                {
                    if (!token.IsValid)
                    {
                        continue;
                    }

                    var value = token.Number;
                    if (!value.HasValue)
                    {
                        continue;
                    }

                    // Retweak tolerance to allow for lengthUnits
                    tolerance = tolerance.ConstrainTolerance(lengthUnits);

                    // Set the clipping for the token's value, based on the token's code, the tolerance and/or the lengthUnits
                    var clip = arcArguments.Any(a => a == token.Code)
                        ? lengthUnits == "mm" ? 3 : 4
                        : tolerance.GetDecimalPlaces();

                    var clipFormat = clip switch
                    {
                        3 => "{0}{1:0.###}",
                        2 => "{0}{1:0.##}",
                        1 => "{0}{1:0.#}",
                        _ => "{0}{1:0.####}",
                    };

                    value = Math.Round(value.Value, clip);
                    token.Source = string.Format(clipFormat, token.Code, value);
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Annotate(this IAsyncEnumerable<Line> tokenisedLines, JObject tokenDefinitions)
        {
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
                    context.BuildContext(tokenDefinitions, token);

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

        private static void BuildContext(this Dictionary<string, string> context, JObject tokenDefinitions, Token token) {
            var replacements = tokenDefinitions["replacements"];

            var replacement = (JObject)replacements[token.Source];
            if (replacement != null)
            {
                foreach (var (ctkey, ctvalue) in replacement)
                {
                    context[ctkey] = (string)ctvalue;
                }
            }
        }
    }
}
