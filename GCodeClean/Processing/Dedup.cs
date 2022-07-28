// Copyright (c) 2020-2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Dedup {
        public static async IAsyncEnumerable<Line> DedupLine(this IAsyncEnumerable<Line> tokenisedLines) {
            var previousLine = new Line();
            await foreach (var line in tokenisedLines) {
                if (previousLine == line)
                {
                    // Silently drop the duplicate
                    continue;
                }

                if (!line.IsNotCommandCodeOrArguments()) {
                    previousLine = new Line(line);
                }

                yield return line;
            }
        }

        /// <summary>
        /// Eliminates redundant context(ual) tokens
        /// </summary>
        /// <param name="tokenisedLines"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> DedupContext(this IAsyncEnumerable<Line> tokenisedLines) {
            var context = Default.Preamble();
            await foreach (var line in tokenisedLines) {
                var contextTokens = context.Lines.SelectMany(l => l.line.Tokens);
                line.AllTokens = line.AllTokens.Except(contextTokens).ToList();

                if (line.AllTokens.Count == 0) {
                    continue;
                }

                context.Update(line);

                yield return line;
            }
        }

        /// <summary>
        /// Eliminates repeated tokens within the same line
        /// </summary>
        public static async IAsyncEnumerable<Line> DedupRepeatedTokens(this IAsyncEnumerable<Line> tokenisedLines) {
            await foreach (var line in tokenisedLines) {
                line.AllTokens = line.AllTokens.Distinct().ToList();
                yield return line;
            }
        }


        /// <summary>
        /// Eliminates superfluous +ve Z, G0 commands
        /// </summary>
        /// <remarks>
        /// This should always go after Processing.ZClamp , because that method corrects G0=>G1 for any lines with -ve Z values
        /// </remarks>
        public static async IAsyncEnumerable<Line> DedupTravelling(this IAsyncEnumerable<Line> tokenisedLines) {
            var lineA = new Line();
            var lineB = new Line();
            var isLineASet = false;
            var isLineBSet = false;

            var travellingMovementToken = new Token("G0");

            await foreach (var lineC in tokenisedLines) {
                var hasMovement = lineC.HasMovementCommand();
                if (hasMovement && !isLineASet && !isLineBSet) {
                    // Some travelling movement command, and we're at a 'start'
                    lineA = new Line(lineC);
                    isLineASet = true;
                    continue;
                }

                var hasTravellingMovement = lineC.Tokens.Contains(travellingMovementToken);
                if (!hasTravellingMovement) {
                    // Not a travelling movement command, therefore
                    if (isLineASet) {
                        yield return lineA;
                        lineA = new Line();
                        isLineASet = false;
                    }
                    if (isLineBSet) {
                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }
                    yield return lineC;
                    continue;
                }

                if (!isLineBSet) {
                    // Set up the B token - this silently drops the previous `lineB`
                    lineB = new Line(lineC);
                    isLineBSet = true;
                }
            }
        }

        /// <summary>
        /// Testing whether A -> B -> C is a straight line
        /// and eliminating B if that's the case
        /// </summary>
        public static async IAsyncEnumerable<Line> DedupLinear(this IAsyncEnumerable<Line> tokenisedLines, decimal tolerance) {
            var lineA = new Line();
            var lineB = new Line();
            var isLineASet = false;
            var isLineBSet = false;

            var linearMovementToken = new Token("G1");

            await foreach (var lineC in tokenisedLines) {
                var hasMovement = lineC.HasMovementCommand();
                var hasLinearMovement = lineC.Tokens.Contains(linearMovementToken);
                if (hasMovement && !isLineASet && !isLineBSet) {
                    // Some movement command, and we're at a 'start'
                    lineA = new Line(lineC);
                    isLineASet = true;
                    continue;
                }
                if (!hasLinearMovement) {
                    // Not a linear movement command
                    if (isLineASet)
                    {
                        yield return lineA;
                        lineA = new Line();
                        isLineASet = false;
                    }
                    if (isLineBSet)
                    {
                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }
                    yield return lineC;
                    continue;
                }
                if (!lineA.IsCompatible(lineC)) {
                    // A linear movement command but A -> C are not of compatible 'form'
                    if (isLineASet)
                    {
                        yield return lineA;
                    }
                    if (isLineBSet) {
                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }
                    lineA = new Line(lineC);
                    isLineASet = true;
                    continue;
                }

                if (!isLineBSet) {
                    // Set up the B token
                    lineB = new Line(lineC);
                    isLineBSet = true;
                    continue;
                }

                Coord coordsA = lineA;
                Coord coordsB = lineB;
                Coord coordsC = lineC;

                // Check we've got a full set of coords for the three token sets
                var hasCoords = (coordsA.Set & coordsB.Set & coordsC.Set) == CoordSet.All;
                var withinBounds = false;
                var isSignificant = false;

                if (hasCoords)
                {
                    // basic check that B is roughly between A and C
                    var xOK = coordsB.X.WithinRange(coordsA.X, coordsC.X);
                    var yOK = coordsB.Y.WithinRange(coordsA.Y, coordsC.Y);
                    var zOK = coordsB.Z.WithinRange(coordsA.Z, coordsC.Z);
                    withinBounds = xOK && yOK && zOK;                 
                }

                if (hasCoords && withinBounds)
                {
                    // Check if any value is too small to matter
                    var distanceAB = Coord.Distance(coordsA, coordsB);
                    var distanceAC = Coord.Distance(coordsA, coordsC);
                    var distanceBC = Coord.Distance(coordsB, coordsC);

                    // Establish whether 'B' is 'significant' and therefore should (probably) not be dropped
                    isSignificant = distanceAB >= tolerance && distanceBC >= tolerance;

                    if (isSignificant)
                    {
                        isSignificant = Math.Abs(distanceAC - (distanceAB + distanceBC)) >= tolerance;
                    }

                    if (isSignificant) {
                        // Heron's formula for area
                        var s = (distanceAB + distanceAC + distanceBC) / 2;
                        var area = (decimal) Math.Sqrt((double) (s * (s - distanceAB) * (s - distanceAC) * (s - distanceBC)));

                        var altitude = 2 * (area / distanceAC);

                        isSignificant = altitude >= tolerance;
                    }
                }

                if (!hasCoords || !withinBounds || isSignificant) {
                    // They are not co-linear! so yield A, and step along (preserving B)
                    yield return lineA;
                    lineA = new Line(lineB);
                }

                lineB = new Line(lineC);
                isLineBSet = true;

                isLineASet = true;
            }
        }

        /// <summary>
        /// Testing whether A -> B -> C can be fitted to an arc
        /// and eliminating B if that's the case
        /// </summary>
        public static async IAsyncEnumerable<Line> DedupLinearToArc(
            this IAsyncEnumerable<Line> tokenisedLines,
            decimal tolerance
        ) {
            var lineA = new Line();
            var lineB = new Line();
            var isLineASet = false;
            var isLineBSet = false;
            var inArc = false;

            var prevCenter = new Coord();
            var prevRadius = 0M;
            var prevIsClockwise = false;

            var linearMovementToken = new Token("G1");

            var context = Default.Preamble();

            await foreach (var lineC in tokenisedLines) {
                context.Update(lineC);

                var hasMovement = lineC.HasMovementCommand();
                var hasLinearMovement = lineC.Tokens.Contains(linearMovementToken);

                if (hasMovement && !isLineASet && !isLineBSet) {
                    // Some movement command, and we're at a 'start'
                    lineA = new Line(lineC);
                    isLineASet = true;
                    continue;
                }

                Coord coordsA = lineA;
                var coordsB = isLineBSet ? lineB : new Coord();
                Coord coordsC = lineC;

                var coordPlane = context.GetCoordPlane();

                if (!hasLinearMovement) {
                    // Not a linear movement command
                    if (isLineASet)
                    {
                        yield return lineA;
                        lineA = new Line();
                        isLineASet = false;
                    }
                    if (isLineBSet)
                    {
                        if (inArc) {
                            lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, coordPlane);
                        }

                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }

                    prevCenter = new Coord();
                    prevRadius = 0M;
                    prevIsClockwise = false;
                    inArc = false;

                    yield return lineC;
                    continue;
                }

                if (!lineA.IsCompatible(lineC)) {
                    // A movement command but A -> C are not of compatible 'form'
                    if (isLineASet)
                    {
                        yield return lineA;
                    }
                    if (isLineBSet) {
                        if (inArc) {
                            lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, coordPlane);
                        }

                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }
                    
                    prevCenter = new Coord();
                    prevRadius = 0M;
                    prevIsClockwise = false;
                    inArc = false;

                    lineA = new Line(lineC);
                    isLineASet = true;
                    continue;
                }

                if (!isLineBSet) {
                    // Set up the B token
                    lineB = new Line(lineC);
                    isLineBSet = true;
                    continue;
                }

                // Check we've got a full set of coords for the three token sets
                var hasCoords = coordsA.Set == coordsB.Set && coordsB.Set == coordsC.Set;
                var withinBounds = false;
                var isSignificant = false;

                if (hasCoords)
                {
                    // basic check that B is roughly between A and C
                    var xOK = coordsB.X.WithinRange(coordsA.X, coordsC.X);
                    var yOK = coordsB.Y.WithinRange(coordsA.Y, coordsC.Y);
                    var zOK = coordsB.Z.WithinRange(coordsA.Z, coordsC.Z);

                    withinBounds = xOK && yOK && zOK;
                }

                var center = new Coord();
                var radius = 0M;
                var isClockwise = false;

                if (hasCoords && withinBounds)
                {
                    // Check if any value is too small to matter
                    var linearToArcTolerance = tolerance.ConstrainTolerance(context.GetLengthUnits()) * 10;

                    var coordsAC = coordsA - coordsC;
                    var coordsAB = coordsA - coordsB;
                    var coordsBC = coordsB - coordsC;

                    var xIsRelevant = coordsAC.X >= linearToArcTolerance && coordsAB.X >= linearToArcTolerance && coordsBC.X >= linearToArcTolerance ? 1 : 0;
                    var yIsRelevant = coordsAC.Y >= linearToArcTolerance && coordsAB.Y >= linearToArcTolerance && coordsBC.Y >= linearToArcTolerance ? 1 : 0;
                    var zIsRelevant = coordsAC.Z >= linearToArcTolerance && coordsAB.Z >= linearToArcTolerance && coordsBC.Z >= linearToArcTolerance ? 1 : 0;

                    isSignificant = coordPlane switch {
                        "G17" => xIsRelevant + yIsRelevant < 2,
                        "G18" => xIsRelevant + zIsRelevant < 2,
                        "G19" => yIsRelevant + zIsRelevant < 2,
                        _ => xIsRelevant + yIsRelevant + zIsRelevant < 2,
                    };

                    if (isSignificant)
                    {
                        (center, radius, isClockwise) = Utility.FindCircle(coordsA, coordsB, coordsC, coordPlane);

                        if (radius > linearToArcTolerance)
                        {
                            var radSqr = radius.Sqr();

                            var bcDistance = (coordsB, coordsC).Distance().Sqr() / 4;
                            var bcr = (double)radius - Math.Sqrt((double)(radSqr - bcDistance));

                            if (inArc)
                            {
                                var centerDiff = Coord.Difference(center, prevCenter);
                                var radiusDiff = Math.Abs(radius - prevRadius);

                                var centerWithinTolerance = coordPlane switch
                                {
                                    "G17" => centerDiff.X <= linearToArcTolerance && centerDiff.Y <= linearToArcTolerance,
                                    "G18" => centerDiff.X <= linearToArcTolerance && centerDiff.Z <= linearToArcTolerance,
                                    "G19" => centerDiff.Y <= linearToArcTolerance && centerDiff.Z <= linearToArcTolerance,
                                    _ => centerDiff.X <= linearToArcTolerance && centerDiff.Y <= linearToArcTolerance && centerDiff.Z <= linearToArcTolerance,
                                };

                                if (centerWithinTolerance && radiusDiff <= linearToArcTolerance)
                                {
                                    isSignificant = bcr <= (double)linearToArcTolerance;
                                }
                                else
                                {
                                    isSignificant = false;
                                }
                            }
                            else
                            {
                                var abDistance = (coordsA, coordsB).Distance().Sqr() / 4;
                                var abr = (double)radius - Math.Sqrt((double)(radSqr - abDistance));

                                isSignificant = abr <= (double)linearToArcTolerance && bcr <= (double)linearToArcTolerance;
                            }
                        }
                        else
                        {
                            isSignificant = false;
                        }
                    }
                }

                if (!hasCoords || !withinBounds || !isSignificant) {
                    yield return lineA;
                    if (!inArc) {
                        lineA = new Line(lineB);
                        lineB = new Line(lineC);

                        isLineASet = true;
                        isLineBSet = true;
                    }
                    else
                    {
                        // Finish the arc manipulation we were doing,
                        // the new arc end point will be lineB
                        lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, coordPlane);

                        prevCenter = new Coord();
                        prevRadius = 0M;
                        prevIsClockwise = false;

                        yield return lineB;
                        lineA = new Line(lineC);
                        isLineASet = true;
                        lineB = new Line();
                        isLineBSet = false;

                        inArc = false;
                    }
                }
                else
                {
                    // They are on an arc! so move things along, silently drop tokenB
                    prevCenter = center;
                    prevRadius = radius;
                    prevIsClockwise = isClockwise;

                    lineB = new Line(lineC);
                    isLineBSet = true;

                    inArc = true;
                }
            }
        }

        private static Line ConvertLinearToArc(this Line lineB, Coord coordsA, Coord prevCenter, bool prevIsClockwise, string coordPlane) {
            var addI = (prevCenter.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X;
            var addJ = (prevCenter.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y;
            var addK = (prevCenter.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z;

            var haveCoordPair = coordPlane switch
            {
                "G17" => addI && addJ,
                "G18" => addI && addK,
                "G19" => addJ && addK,
                _ => addI && addJ && addK,
            };

            if (!haveCoordPair)
            {
                return lineB;
            }

            var linearMovementToken = new Token("G1");
            lineB.RemoveToken(linearMovementToken);
            lineB.PrependToken(new Token(prevIsClockwise ? "G2" : "G3"));

            var centerX = new Token($"I{prevCenter.X - coordsA.X:0.####}");
            var centerY = new Token($"J{prevCenter.Y - coordsA.Y:0.####}");
            var centerZ = new Token($"K{prevCenter.Z - coordsA.Z:0.####}");

            switch (coordPlane)
            {
                case "G17":
                    lineB.AppendToken(centerX);
                    lineB.AppendToken(centerY);
                    if (addK && prevCenter.Z - coordsA.Z != 0)
                    {
                        lineB.AppendToken(centerZ);
                    }
                    break;
                case "G18":
                    lineB.AppendToken(centerX);
                    if (addJ && prevCenter.Y - coordsA.Y != 0)
                    {
                        lineB.AppendToken(centerY);
                    }
                    lineB.AppendToken(centerZ);
                    break;
                case "G19":
                    if (addI && prevCenter.X - coordsA.X != 0)
                    {
                        lineB.AppendToken(centerX);
                    }
                    lineB.AppendToken(centerY);
                    lineB.AppendToken(centerZ);
                    break;
                default:
                    lineB.AppendToken(centerX);
                    lineB.AppendToken(centerY);
                    lineB.AppendToken(centerZ);
                    break;
            }

            return lineB;
        }

        public static async IAsyncEnumerable<Line> DedupSelectTokens(this IAsyncEnumerable<Line> tokenisedLines, List<char> selectedTokens) {
            var previousSelectedTokens = selectedTokens.Select(st => new Token($"{st}")).ToList();

            await foreach (var line in tokenisedLines) {
                if (line.IsNotCommandCodeOrArguments()) {
                    yield return line;
                    continue;
                }

                line.RemoveTokens(previousSelectedTokens);

                for (var ix = 0; ix < previousSelectedTokens.Count; ix++) {
                    var newToken = line.AllTokens.FirstOrDefault(t => t.Code == previousSelectedTokens[ix].Code);
                    if (newToken != null) {
                        previousSelectedTokens[ix] = newToken;
                    }
                }

                if (line.Tokens.Count == 0)
                {
                    // The whole line (ignoring line numbers) was eliminated as a duplicate - silently drop it
                    continue;
                }

                yield return line;
            }
        }
    }
}
