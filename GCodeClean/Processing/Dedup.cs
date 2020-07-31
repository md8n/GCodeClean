// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
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
                    previousLine = line;
                }

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
                    lineA = lineC;
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
                    lineA = lineC;
                    isLineASet = true;
                    continue;
                }

                if (!isLineBSet) {
                    // Set up the B token
                    lineB = lineC;
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
                    var coordsAC = coordsA - coordsC;
                    var coordsAB = coordsA - coordsB;
                    var coordsBC = coordsB - coordsC;

                    var xIsRelevant = coordsAC.X >= tolerance && coordsAB.X >= tolerance && coordsBC.X >= tolerance ? 1 : 0;
                    var yIsRelevant = coordsAC.Y >= tolerance && coordsAB.Y >= tolerance && coordsBC.Y >= tolerance ? 1 : 0;
                    var zIsRelevant = coordsAC.Z >= tolerance && coordsAB.Z >= tolerance && coordsBC.Z >= tolerance ? 1 : 0;

                    isSignificant = xIsRelevant + yIsRelevant + zIsRelevant < 2;

                    var xyIsSignificant = false;
                    var xzIsSignificant = false;
                    var yzIsSignificant = false;

                    if (isSignificant) {
                        var acXYAngle = (coordsAC.X, coordsAC.Y).Angle();
                        var abXYAngle = (coordsAB.X, coordsAB.Y).Angle();

                        var acXZAngle = (coordsAC.X, coordsAC.Z).Angle();
                        var abXZAngle = (coordsAB.X, coordsAB.Z).Angle();

                        var acYZAngle = (coordsAC.Y, coordsAC.Z).Angle();
                        var abYZAngle = (coordsAB.Y, coordsAB.Z).Angle();

                        if (xIsRelevant + yIsRelevant == 2)
                        {
                            xyIsSignificant = Math.Abs(acXYAngle - abXYAngle) >= tolerance;
                        }
                        if (xIsRelevant + zIsRelevant == 2)
                        {
                            xzIsSignificant = Math.Abs(acXZAngle - abXZAngle) >=tolerance;
                        }
                        if (yIsRelevant + zIsRelevant == 2)
                        {
                            yzIsSignificant = Math.Abs(acYZAngle - abYZAngle) >= tolerance;
                        }
                    }

                    isSignificant = xyIsSignificant || xzIsSignificant || yzIsSignificant;
                }

                yield return lineA;
                if (!hasCoords || !withinBounds || !isSignificant) {
                    yield return lineB;
                }
                // else - They are co-linear! so move things along and silently drop tokenB

                lineA = lineC;
                isLineASet = true;
                lineB = new Line();
                isLineBSet = false;
            }
        }

        /// <summary>
        /// Testing whether A -> B -> C can be fitted to an arc
        /// and eliminating B if that's the case
        /// </summary>
        public static async IAsyncEnumerable<Line> DedupLinearToArc(this IAsyncEnumerable<Line> tokenisedLines, Context context, decimal tolerance) {
            var lineA = new Line();
            var lineB = new Line();
            var isLineASet = false;
            var isLineBSet = false;
            var inArc = false;

            var prevCenter = new Coord();
            var prevRadius = 0M;
            var prevIsClockwise = false;

            var linearMovementToken = new Token("G1");

            await foreach (var lineC in tokenisedLines) {
                var hasMovement = lineC.HasMovementCommand();
                var hasLinearMovement = lineC.Tokens.Contains(linearMovementToken);

                context.Update(lineC, true);

                if (hasMovement && !isLineASet && !isLineBSet) {
                    // Some movement command, and we're at a 'start'
                    lineA = lineC;
                    isLineASet = true;
                    continue;
                }

                Coord coordsA = lineA;
                var coordsB = isLineBSet ? lineB : new Coord();
                Coord coordsC = lineC;

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
                            lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, context);
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
                            lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, context);
                        }

                        yield return lineB;
                        lineB = new Line();
                        isLineBSet = false;
                    }
                    
                    prevCenter = new Coord();
                    prevRadius = 0M;
                    prevIsClockwise = false;
                    inArc = false;

                    lineA = lineC;
                    isLineASet = true;
                    continue;
                }

                if (!isLineBSet) {
                    // Set up the B token
                    lineB = lineC;
                    isLineBSet = true;
                    continue;
                }

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

                var center = new Coord();
                var radius = 0M;
                var isClockwise = false;

                if (hasCoords && withinBounds)
                {
                    // Check if any value is too small to matter                    
                    var coordsAC = coordsA - coordsC;
                    var coordsAB = coordsA - coordsB;
                    var coordsBC = coordsB - coordsC;

                    var xIsRelevant = coordsAC.X >= tolerance && coordsAB.X >= tolerance && coordsBC.X >= tolerance ? 1 : 0;
                    var yIsRelevant = coordsAC.Y >= tolerance && coordsAB.Y >= tolerance && coordsBC.Y >= tolerance ? 1 : 0;
                    var zIsRelevant = coordsAC.Z >= tolerance && coordsAB.Z >= tolerance && coordsBC.Z >= tolerance ? 1 : 0;

                    var coordPlane = context.GetModalState(ModalGroup.ModalPlane).ToString();

                    isSignificant = coordPlane switch
                    {
                        "G17" => xIsRelevant + yIsRelevant < 2,
                        "G18" => xIsRelevant + zIsRelevant < 2,
                        "G19" => yIsRelevant + zIsRelevant < 2,
                        _ => xIsRelevant + yIsRelevant + zIsRelevant < 2,
                    };

                    if (isSignificant)
                    {
                        (center, radius, isClockwise) = Utility.FindCircle(coordsA, coordsB, coordsC, context);

                        if (radius > tolerance)
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
                                    "G17" => centerDiff.X <= tolerance && centerDiff.Y <= tolerance,
                                    "G18" => centerDiff.X <= tolerance && centerDiff.Z <= tolerance,
                                    "G19" => centerDiff.Y <= tolerance && centerDiff.Z <= tolerance,
                                    _ => centerDiff.X <= tolerance && centerDiff.Y <= tolerance && centerDiff.Z <= tolerance,
                                };

                                if (centerWithinTolerance && radiusDiff <= tolerance)
                                {
                                    isSignificant = bcr <= (double)tolerance;
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

                                isSignificant = abr <= (double)tolerance && bcr <= (double)tolerance;
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
                        lineA = lineB;
                        lineB = lineC;

                        isLineASet = true;
                        isLineBSet = true;
                    }
                    else
                    {
                        // Finish the arc manipulation we were doing,
                        // the new arc end point will be lineB
                        lineB = lineB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise, context);

                        prevCenter = new Coord();
                        prevRadius = 0M;
                        prevIsClockwise = false;

                        yield return lineB;
                        lineA = lineC;
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

                    lineB = lineC;
                    isLineBSet = true;

                    inArc = true;
                }
            }
        }

        private static Line ConvertLinearToArc(this Line lineB, Coord coordsA, Coord prevCenter, bool prevIsClockwise, Context context) {
            var addI = (prevCenter.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X;
            var addJ = (prevCenter.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y;
            var addK = (prevCenter.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z;

            var modalPlace = context.GetModalState(ModalGroup.ModalPlane).ToString();
            var haveCoordPair = modalPlace switch
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
            for (var ix = 0; ix < lineB.AllTokens.Count; ix++)
            {
                if (lineB.AllTokens[ix] != linearMovementToken)
                {
                    continue;
                }

                lineB.AllTokens[ix] = new Token(prevIsClockwise ? "G2" : "G3");
                break;
            }

            switch (modalPlace)
            {
                case "G17":
                    lineB.AppendToken(new Token($"I{prevCenter.X - coordsA.X:0.####}"));
                    lineB.AppendToken(new Token($"J{prevCenter.Y - coordsA.Y:0.####}"));
                    if (addK && prevCenter.Z - coordsA.Z != 0)
                    {
                        lineB.AppendToken(new Token($"K{prevCenter.Z - coordsA.Z:0.####}"));
                    }
                    break;
                case "G18":
                    lineB.AppendToken(new Token($"I{prevCenter.X - coordsA.X:0.####}"));
                    if (addJ && prevCenter.Y - coordsA.Y != 0)
                    {
                        lineB.AppendToken(new Token($"J{prevCenter.Y - coordsA.Y:0.####}"));
                    }
                    lineB.AppendToken(new Token($"K{prevCenter.Z - coordsA.Z:0.####}"));
                    break;
                case "G19":
                    if (addI && prevCenter.X - coordsA.X != 0)
                    {
                        lineB.AppendToken(new Token($"I{prevCenter.X - coordsA.X:0.####}"));
                    }
                    lineB.AppendToken(new Token($"J{prevCenter.Y - coordsA.Y:0.####}"));
                    lineB.AppendToken(new Token($"K{prevCenter.Z - coordsA.Z:0.####}"));
                    break;
                default:
                    lineB.AppendToken(new Token($"I{prevCenter.X - coordsA.X:0.####}"));
                    lineB.AppendToken(new Token($"J{prevCenter.Y - coordsA.Y:0.####}"));
                    lineB.AppendToken(new Token($"K{prevCenter.Z - coordsA.Z:0.####}"));
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
