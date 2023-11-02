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
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<Context> BuildPreamble(
            this IAsyncEnumerable<Line> tokenisedLines,
            CancellationToken cancellationToken = default
        ) {
            var preamble = Default.Preamble();
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
            var premableCompletionByGCodeClean = false;
            var zClampConstrained = Utility.ConstrictZClamp(preamble.GetLengthUnits(), zClamp);

            await foreach (var line in tokenisedLines) {
                if (line.HasTokens(ModalGroup.ModalAllMotion)) {
                    var linesToOutput = preamble.NonOutputLines();
                    if (linesToOutput.Count > 0) {
                        premableCompletionByGCodeClean = true;
                        linesToOutput.Insert(0, new Line("(Preamble completion by GCodeClean)"));
                        linesToOutput.Add(new Line("(Preamble completed by GCodeClean)"));
                        linesToOutput.Add(new Line(""));
                        // Inject a +ve Z after the preamble, and before or with the movement
                        if (line.HasToken("G0")) {
                            if (line.Tokens.Count == 2 && line.HasToken('Z')) {
                                line.RemoveTokens(new List<char> { 'Z' });
                            }
                            if (line.Tokens.Count == 1) {
                                line.AppendToken(new Token($"Z{zClampConstrained}"));
                            } else {
                                linesToOutput.Add(new Line($"G0 Z{zClampConstrained}"));
                            }
                        } else {
                            linesToOutput.Add(new Line($"G0 Z{zClampConstrained}"));
                        }
                        foreach (var preambleLine in linesToOutput) {
                            yield return preambleLine;
                        }
                    }
                    if (!preamble.AllLinesOutput) {
                        preamble.FlagAllLinesAsOutput();
                        if (!premableCompletionByGCodeClean) {
                            yield return new Line("(Preamble completed)");
                            yield return new Line("");
                        }
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

            var context = Default.Preamble();

            await foreach (var line in tokenisedLines) {
                context.Update(line);

                var hasZ = line.HasToken('Z');
                var hasTravelling = line.HasTokens(ModalGroup.ModalAllMotion);

                if (hasZ && hasTravelling) {
                    var zToken = line.AllTokens.First(t => t.Code == 'Z');
                    currentZ = zToken.Number.Value;
                }

                if (!leadingBlankLinesStripped) {
                    if (line.AllTokens.Count == 0) {
                        // Strip the leading blank line
                        continue;
                    } else {
                        leadingBlankLinesStripped = true;
                        hasLeadingFileTerminator = line.HasToken('%');
                    }
                } else {
                    if (line.HasToken('%')) {
                        commentOutAllRemainingCommands = true;
                        if (!hasLeadingFileTerminator)
                        {
                            // throw away the trailing terminator, because there is no leading one
                            // Note that all commands after this point are still commented out on the assumption that 
                            // the mismatched trailling terminator was intentional.
                            continue;
                        } else {
                            hasTrailingFileTerminator = true;
                        }
                    }
                    if (line.HasTokens(ModalGroup.ModalStopping)) {
                        hasStopping = true;
                        if (!commentOutAllRemainingCommands) {
                            commentOutAllRemainingCommands = true;

                            if (currentZ < 0) {
                                // If the current tool height is less than zero then it needs to be raised
                                // before the stop command
                                var zClampConstrained = Utility.ConstrictZClamp(context.GetLengthUnits(), zClamp);
                                yield return new Line($"G0 Z{zClampConstrained}");
                            }

                            yield return line;
                            continue;
                        }
                    }
                }

                if (commentOutAllRemainingCommands) {
                    line.AllTokens = line.AllTokens.Select(t => t.ToComment()).ToList();
                }

                yield return line;
            }

            if (hasLeadingFileTerminator && !hasTrailingFileTerminator)
            {
                yield return new Line("(Postamble completed by GCodeClean)");
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
                yield return new Line("(Postamble completed by GCodeClean)");
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
                            if (!ModalGroup.ModalAllMotion.Contains(token))
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
                    previousXYZCoords[ix] = line.Tokens.Find(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
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
        ) {
            var context = Default.Preamble();
            var prevX = 0M;
            var prevY = 0M;
            var prevZ = zClamp;

            await foreach (var line in tokenisedLines) {
                context.Update(line);

                if (line.IsNotCommandCodeOrArguments()) {
                    yield return line;
                    continue;
                }

                var hasX = line.HasToken('X');
                var hasY = line.HasToken('Y');
                var hasZ = line.HasToken('Z');
                var hasTravelling = line.HasTokens(ModalGroup.ModalSimpleMotion);

                if (hasZ && hasTravelling) {
                    var zToken = line.AllTokens.First(t => t.Code == 'Z');

                    var travelingToken = line.AllTokens.Intersect(ModalGroup.ModalSimpleMotion).First();

                    if (zToken.Number >= 0) {
                        // If Z >= 0 then the z value should be constrained
                        var zClampConstrained = Utility.ConstrictZClamp(context.GetLengthUnits(), zClamp);
                        zToken.Number = zClampConstrained;
                        var xUnchanged = !hasX || line.AllTokens.First(t => t.Code == 'X').Number.Value == prevX;
                        var yUnchanged = !hasY || line.AllTokens.First(t => t.Code == 'Y').Number.Value == prevY;
                        if (prevZ >= 0 || (xUnchanged || yUnchanged)) {
                            // If the previous Z value is also >= 0 or there's no X or Y movement then the motion should be G0
                            travelingToken.Source = "G0";
                        }
                    } else if (travelingToken.Source == "G0") {
                        // If Z < 0 and source is G0 then the motion should be G1 (probably)
                        travelingToken.Source = "G1";
                    }

                    prevX = hasX ? line.AllTokens.First(t => t.Code == 'X').Number.Value : prevX;
                    prevY = hasY ? line.AllTokens.First(t => t.Code == 'Y').Number.Value : prevY;
                    prevZ = zToken.Number.Value;
                }

                yield return line;
            }
        }

        /// <summary>
        /// Convert Arc movement commands from using R to using IJ
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> ConvertArcRadiusToCenter(
            this IAsyncEnumerable<Line> tokenisedLines
        )
        {
            var previousCoords = new Coord();
            var clockwiseMovementToken = new Token("G2");
            var context = Default.Preamble();

            await foreach (var line in tokenisedLines)
            {
                context.Update(line);

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

                var radius = line.Tokens.Find(t => t.Code == 'R')?.Number;
                if (!radius.HasValue || !coords.HasCoordPair())
                {
                    previousCoords = Coord.Merge(previousCoords, coords, true);

                    yield return line;
                    continue;
                }

                var coordPlane = context.GetCoordPlane();
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
            decimal arcTolerance = 0.0005M
        )
        {
            var previousCommand = new Token("");
            var previousXYZCoords = new List<Token> { new Token("X"), new Token("Y"), new Token("Z") };
            var arcArguments = new List<char> { 'I', 'J', 'K' };
            var arcCommands = new List<Token> {
                new Token("G2"), new Token("G3")
            };
            var context = Default.Preamble();

            await foreach (var line in tokenisedLines)
            {
                context.Update(line);
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
                        previousXYZCoords[ix] = line.Tokens.Find(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                    }

                    yield return line;
                    continue;
                }

                Coord coordsA = new Line(previousXYZCoords);
                Coord coordsB = line;
                var abDistance = (coordsA, coordsB).Distance();
                arcTolerance = arcTolerance.ConstrainTolerance(context.GetLengthUnits());
                if (abDistance <= arcTolerance)
                {
                    line.RemoveTokens(arcArguments);
                    line.RemoveTokens(arcCommands);
                    line.PrependToken(new Token("G1"));
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++)
                {
                    previousXYZCoords[ix] = line.Tokens.Find(t => t.Code == previousXYZCoords[ix].Code) ?? previousXYZCoords[ix];
                }

                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> Clip(
            this IAsyncEnumerable<Line> tokenisedLines,
            decimal tolerance = 0.0005M
        )
        {
            var arcArguments = new[] { 'I', 'J', 'K' };

            var context = Default.Preamble();

            await foreach (var line in tokenisedLines)
            {
                context.Update(line);

                if (line.IsNotCommandCodeOrArguments())
                {
                    yield return line;
                    continue;
                }

                // Set the clipping based on the lengthUnits
                var lengthUnits = context.GetLengthUnits();
                var unitClip = lengthUnits == "mm" ? 3 : 4;

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

        /// <summary>
        /// Inject 'travelling dividers' to show where travelling is occurring
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> DetectTravelling(this IAsyncEnumerable<Line> tokenisedLines) {
            var context = Default.Preamble();
            var isTravelling = true; // Assuming that things start with +ve z-axis value
            var entryLine = new Line();
            var exitLine = new Line();
            var entrySet = false;
            var blockIx = 0;
            Line travellingLine;

            await foreach (var line in tokenisedLines) {
                context.Update(line);

                if (line.HasToken('X') || line.HasToken('Y')) {
                    if (!entrySet) {
                        entryLine = new Line(line);
                        entrySet = true;
                    }
                    exitLine = new Line(line);
                }

                if (line.HasToken('Z')) {
                    var zToken = line.AllTokens.First(t => t.Code == 'Z');
                    if (zToken.Number >= 0) {
                        if (!isTravelling) {
                            travellingLine = new Line(line);
                            travellingLine.ReplaceToken(new Token("G1"), new Token("G0"));

                            line.AppendToken(new Token($"(||Travelling||{blockIx++}||>>{entryLine}>>{exitLine}>>||)"));
                            entryLine = new Line();
                            exitLine = new Line();
                            entrySet = false;
                        }

                        if (!isTravelling) {
                            isTravelling = true;
                        }
                    } else {
                        isTravelling = false;
                    }
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
            var annotationContext = new Dictionary<string, string>();

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
                    annotationContext.BuildAnnotation(tokenDefinitions, token);

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
                        annotationContext[token.Code + "value"] = token.Number.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        tokenCodes.Add(token.ToString());
                    }

                    if (annotation == null)
                    {
                        continue;
                    }

                    foreach (var (key, value) in annotationContext)
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

        private static void BuildAnnotation(
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
