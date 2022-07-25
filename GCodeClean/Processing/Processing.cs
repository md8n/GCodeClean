// Copyright (c) 2020-2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Processing
    {
        /// <summary>
        /// Build the `preamble` from the default Context and
        /// whatever is supplied in the GCode before the first motion command
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <param name="preamble"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<Context> BuildPreamble(
            this IAsyncEnumerable<Line> tokenisedLines,
            Context preamble,
            CancellationToken cancellationToken = default
        ) {
            foreach (var line in await tokenisedLines.ToListAsync(cancellationToken)) {
                if (line.HasTokens(ModalGroup.ModalAllMotion)) {
                    break;
                }
                preamble.Update(line, true);
            }

            return preamble;
        }

        public static async IAsyncEnumerable<Line> InjectPreamble(
            this IAsyncEnumerable<Line> tokenisedLines,
            Context preamble,
            decimal zClamp = 10.0M
        ) {
            await foreach (var line in tokenisedLines) {
                if (line.HasTokens(ModalGroup.ModalAllMotion)) {
                    var linesToOutput = preamble.NonOutputLines();
                    if (linesToOutput.Count > 0) {
                        linesToOutput.Insert(0, new Line("(Preamble completed by GCodeClean)"));
                        // If the line is a G0 movement then inject a +ve Z
                        if (line.HasToken("G0")) {
                            linesToOutput.Add(new Line($"Z{zClamp}"));
                        }
                        linesToOutput.Add(new Line("(Preamble completed by GCodeClean)"));
                        foreach (var preambleLine in linesToOutput) {
                            yield return preambleLine;
                        }
                    }
                    if (!preamble.AllLinesOutput) {
                        preamble.FlagAllLinesAsOutput();
                    }
                }

                yield return line;
            }
        }

        /// <summary>
        /// Ensure that file terminations meet the rules
        /// </summary>
        /// <remarks>In effect this is also `InjectPostamble`</remarks>
        /// <param name="tokenisedLines"></param>
        /// <param name="preamble"></param>
        /// <param name="zClamp"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> FileDemarcation(
            this IAsyncEnumerable<Line> tokenisedLines,
            decimal zClamp = 10.0M
        )
        {
            var currentZ = -1M; // Arbitrary
            var leadingBlankLinesStripped = false;
            var hasLeadingFileTerminator = false;
            var hasTrailingFileTerminator = false;
            var hasStopping = false;
            var commentOutAllRemainingCommands = false;

            await foreach (var line in tokenisedLines)
            {
                var hasZ = line.HasToken('Z');
                var hasTraveling = line.HasTokens(ModalGroup.ModalSimpleMotion);

                if (hasZ && hasTraveling)
                {
                    var zToken = line.AllTokens.First(t => t.Code == 'Z');
                    currentZ = zToken.Number.Value;
                }

                if (!leadingBlankLinesStripped)
                {
                    if (line.AllTokens.Count == 0)
                    {
                        // Strip the leading blank line
                        continue;
                    }
                    else
                    {
                        leadingBlankLinesStripped = true;
                        hasLeadingFileTerminator = line.HasToken('%');
                    }
                }
                else
                {
                    if (line.HasToken('%'))
                    {
                        commentOutAllRemainingCommands = true;
                        if (!hasLeadingFileTerminator)
                        {
                            // throw away the trailing terminator, because there is no leading one
                            // Note that all commands after this point are still commented out on the assumption that 
                            // the mismatched trailling terminator was intentional.
                            continue;
                        }
                        else
                        {
                            hasTrailingFileTerminator = true;
                        }
                    }
                    if (line.HasTokens(ModalGroup.ModalStopping))
                    {
                        hasStopping = true;
                        if (!commentOutAllRemainingCommands)
                        {
                            commentOutAllRemainingCommands = true;

                            if (currentZ < 0)
                            {
                                // If the current tool height is less than zero then it needs to be raised
                                // before the stop command
                                yield return new Line($"G0 Z{zClamp}");
                            }

                            yield return line;
                            continue;
                        }
                    }
                }

                if (commentOutAllRemainingCommands)
                {
                    line.AllTokens = line.AllTokens.Select(t => t.ToComment()).ToList();
                }

                yield return line;
            }

            if (hasLeadingFileTerminator && !hasTrailingFileTerminator)
            {
                // Inject a file demarcation character
                yield return new Line("%");
            }
            else if (!hasLeadingFileTerminator && !hasStopping)
            {
                if (currentZ < 0)
                {
                    // If the current tool height is less than zero then it needs to be raised
                    // before the stop command
                    yield return new Line($"G0 Z{zClamp}");
                }
                // Inject a full stop - M30 used in preference to M2
                yield return new Line("M30");
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
                        line.PrependToken(previousCommand);
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
                    line.AppendTokens(previousXYZCoords.Where(pc => pc.IsArgument && pc.IsValid));

                    line.AppendTokens(line.RemoveTokens(new List<char> { 'I' }));
                    line.AppendTokens(line.RemoveTokens(new List<char> { 'J' }));
                    line.AppendTokens(line.RemoveTokens(new List<char> { 'K' }));
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

        /// <summary>
        /// Constrict any supplied tolerances etc. according to the selected units
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <param name="preamble"></param>
        /// <param name="zClamp"></param>
        /// <returns></returns>
        public static async Task<decimal> ConstrictValues(
            this IAsyncEnumerable<Line> tokenisedLines,
            Context preamble,
            decimal zClamp = 10.0M,
            CancellationToken cancellationToken = default
        )
        {
            foreach (var line in await tokenisedLines.ToListAsync(cancellationToken))
            {
                preamble.Update(line, true);

                var lengthUnits = Utility.GetLengthUnits(preamble);
                zClamp = Utility.ConstrictZClamp(lengthUnits, zClamp);
            }

            return zClamp;
        }

        public static async IAsyncEnumerable<Line> ZClamp(
            this IAsyncEnumerable<Line> tokenisedLines,
            decimal zClamp = 10.0M
        )
        {
            await foreach (var line in tokenisedLines)
            {
                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                var hasZ = line.HasToken('Z');
                var hasTraveling = line.HasTokens(ModalGroup.ModalSimpleMotion);

                if (hasZ && hasTraveling)
                {
                    var zToken = line.AllTokens.First(t => t.Code == 'Z');

                    if (zToken.Number > 0)
                    {
                        zToken.Number = zClamp;

                        foreach (var travelingToken in line.AllTokens.Intersect(ModalGroup.ModalSimpleMotion))
                        {
                            // If Z > 0 then the motion should be G0 
                            travelingToken.Source = "G0";
                        }
                    }
                }

                yield return line;
            }
        }

        // public static async IAsyncEnumerable<Line> ZClean(this IAsyncEnumerable<Line> tokenisedLines)
        // {
        //     var previousCoords = new Coord();

        //     await foreach (var line in tokenisedLines)
        //     {
        //         var hasMovement = line.HasMovementCommand();
        //         if (!hasMovement)
        //         {
        //             yield return line;
        //             continue;
        //         }

        //         Coord coords = line;
        //         if (!previousCoords.HasCoordPair())
        //         {
        //             // Some movement command, and we're at a 'start'
        //             previousCoords = Coord.Merge(previousCoords, coords, true);

        //             yield return line;
        //             continue;
        //         }

        //         var hasZ = line.HasToken('Z');
        //         var hasTraveling = line.HasTokens(ModalGroup.ModalSimpleMotion);

        //         if (hasZ && hasTraveling)
        //         {
        //             var zTokenIndex = line.AllTokens.FindIndex(t => t.Code == 'Z');

        //             if (line.AllTokens[zTokenIndex].Number > 0) {
        //                 line.AllTokens[zTokenIndex].Number = zClamp;

        //                 foreach (var travelingToken in line.AllTokens.Intersect(ModalGroup.ModalSimpleMotion))
        //                 {
        //                     // If Z > 0 then the motion should be G0 
        //                     travelingToken.Source = "G0";
        //                 }
        //             }
        //         }

        //         yield return line;
        //     }
        // }

        /// <summary>
        /// Convert Arc movement commands from using R to using IJ
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <param name="coordPlane">The coordinate plane as identified by the preamble</param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> ConvertArcRadiusToCenter(
            this IAsyncEnumerable<Line> tokenisedLines,
            string coordPlane
        )
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

                var intersections = Utility.FindIntersections(coords, previousCoords, radius.Value, coordPlane);
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

        public static async IAsyncEnumerable<Line> SimplifyShortArcs(
            this IAsyncEnumerable<Line> tokenisedLines,
            string lengthUnits = "mm",
            decimal arcTolerance = 0.0005M
        )
        {
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token> { new Token("X"), new Token("Y"), new Token("Z") };
            var arcArguments = new List<char> { 'I', 'J', 'K' };
            var arcCommands = new List<Token> {
                new Token("G2"), new Token("G3")
            };

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
                        line.PrependToken(previousCommand);
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
                arcTolerance = arcTolerance.ConstrainTolerance(lengthUnits);
                if (abDistance <= arcTolerance)
                {
                    line.RemoveTokens(arcArguments);
                    line.RemoveTokens(arcCommands);
                    line.PrependToken(new Token("G1"));
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    previousXYZCoords[ix] = line.Tokens.FirstOrDefault(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Clip(
            this IAsyncEnumerable<Line> tokenisedLines,
            string lengthUnits,
            decimal tolerance = 0.0005M
        )
        {
            var arcArguments = new[] { 'I', 'J', 'K' };

            // Set the clipping based on the lengthUnits
            var unitClip = lengthUnits == "mm" ? 3 : 4;

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

                    var value = token.Number;
                    if (!value.HasValue)
                    {
                        continue;
                    }

                    // Re-tweak tolerance to allow for lengthUnits
                    tolerance = tolerance.ConstrainTolerance(lengthUnits);

                    // Set the clipping for the token's value, based on the token's code, the tolerance and/or the lengthUnits
                    var clip = arcArguments.Any(a => a == token.Code)
                        ? unitClip
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

        public static async IAsyncEnumerable<Line> Annotate(
            this IAsyncEnumerable<Line> tokenisedLines,
            JsonElement tokenDefinitions
        )
        {
            var tokenDefs = tokenDefinitions.GetProperty("tokenDefs");
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

                    string annotation = null;
                    if (tokenDefs.TryGetProperty(token.ToString(), out var tokenDef))
                    {
                        annotation = tokenDef.GetString();
                    }
                    if (annotation is null && token.Number.HasValue)
                    {
                        var subToken = "" + token.Code;
                        tokenCodes.Add(subToken);
                        if (tokenDefs.TryGetProperty(subToken, out var subTokenDef))
                        {
                            annotation = subTokenDef.GetString();
                        }
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
                    line.AppendToken(new Token($"({string.Join(", ", annotationTokens)})"));
                    previousTokenCodes = tokenCodes;
                }

                yield return line;
            }
        }

        private static Line ArcRadiusToCenter(this Line lineB, Coord coordsA, Coord center)
        {
            lineB.RemoveTokens(new List<char> { 'R' });

            if ((center.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X)
            {
                lineB.AppendToken(new Token($"I{center.X - coordsA.X:0.####}"));
            }
            if ((center.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y)
            {
                lineB.AppendToken(new Token($"J{center.Y - coordsA.Y:0.####}"));
            }
            if ((center.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z)
            {
                lineB.AppendToken(new Token($"K{center.Z - coordsA.Z:0.####}"));
            }

            return lineB;
        }

        private static void BuildContext(
            this Dictionary<string, string> context,
            JsonElement tokenDefinitions,
            Token token
        )
        {
            var replacements = tokenDefinitions.GetProperty("replacements");

            if (!replacements.TryGetProperty(token.Source, out var replacement))
            {
                return;
            }

            foreach (var ct in replacement.EnumerateObject())
            {
                context[ct.Name] = ct.Value.GetString();
            }
        }
    }
}
