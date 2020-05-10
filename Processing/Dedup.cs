// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Processing
{
    public static class Dedup {
        public static async IAsyncEnumerable<List<string>> DedupLine(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var previousTokens = new List<string>();
            await foreach (var tokens in tokenizedLines) {
                if (!previousTokens.AreTokensEqual(tokens)) {
                    if (!tokens.IsNotCommandOrArguments()) {
                        previousTokens = tokens;
                    }

                    yield return tokens;
                }

                // Silently drop the duplicate
            }
        }

        /// <summary>
        /// Eliminates repeated tokens within the same line
        /// </summary>
        public static async IAsyncEnumerable<List<string>> DedupRepeatedTokens(this IAsyncEnumerable<List<string>> tokenizedLines) {
            await foreach (var tokens in tokenizedLines) {
                var distinctTokens = tokens.Distinct().ToList();
                yield return distinctTokens;
            }
        }


        /// <summary>
        /// Testing whether A -> B -> C is a straight line
        /// and eliminating B if that's the case
        /// </summary>
        public static async IAsyncEnumerable<List<string>> DedupLinear(this IAsyncEnumerable<List<string>> tokenizedLines, decimal tolerance) {
            var tokensA = new List<string>();
            var tokensB = new List<string>();
            var areTokensASet = false;
            var areTokensBSet = false;

            await foreach (var tokensC in tokenizedLines) {
                var hasMovement = tokensC.HasMovementCommand();
                var hasLinearMovement = tokensC.Any(tc => new []{"G1", "G01"}.Contains(tc));
                if (hasMovement && !areTokensASet && !areTokensBSet) {
                    // Some movement command, and we're at a 'start'
                    tokensA = tokensC;
                    areTokensASet = true;
                    continue;
                }
                if (!hasLinearMovement) {
                    // Not a linear movement command
                    if (areTokensASet)
                    {
                        yield return tokensA;
                        tokensA = new List<string>();
                        areTokensASet = false;
                    }
                    if (areTokensBSet)
                    {
                        yield return tokensB;
                        tokensB = new List<string>();
                        areTokensBSet = false;
                    }
                    yield return tokensC;
                    continue;
                }
                if (!tokensA.AreTokensCompatible(tokensC)) {
                    // A linear movement command but A -> C are not of compatible 'form'
                    if (areTokensASet)
                    {
                        yield return tokensA;
                    }
                    if (areTokensBSet) {
                        yield return tokensB;
                        tokensB = new List<string>();
                        areTokensBSet = false;
                    }
                    tokensA = tokensC;
                    areTokensASet = true;
                    continue;
                }

                if (!areTokensBSet) {
                    // Set up the B token
                    tokensB = tokensC;
                    areTokensBSet = true;
                    continue;
                }

                var coordsA = tokensA.ExtractCoords();
                var coordsB = tokensB.ExtractCoords();
                var coordsC = tokensC.ExtractCoords();

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

                    var xIsRelevant = (coordsAC.X >= tolerance && coordsAB.X >= tolerance && coordsBC.X >= tolerance) ? 1 : 0;
                    var yIsRelevant = (coordsAC.Y >= tolerance && coordsAB.Y >= tolerance && coordsBC.Y >= tolerance) ? 1 : 0;
                    var zIsRelevant = (coordsAC.Z >= tolerance && coordsAB.Z >= tolerance && coordsBC.Z >= tolerance) ? 1 : 0;

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

                yield return tokensA;
                if (!hasCoords || !withinBounds || !isSignificant) {
                    yield return tokensB;
                }
                // else - They are colinear! so move things along and silently drop tokenB

                tokensA = tokensC;
                areTokensASet = true;
                tokensB = new List<string>();
                areTokensBSet = false;
            }
        }


        /// <summary>
        /// Testing whether A -> B -> C can be fitted to an arc
        /// and eliminating B if that's the case
        /// </summary>
        public static async IAsyncEnumerable<List<string>> DedupLinearToArc(this IAsyncEnumerable<List<string>> tokenizedLines, decimal tolerance) {
            var tokensA = new List<string>();
            var tokensB = new List<string>();
            var areTokensASet = false;
            var areTokensBSet = false;
            var inArc = false;

            var prevCenter = new Coord();
            var prevRadius = 0M;
            var prevIsClockwise = false;

            await foreach (var tokensC in tokenizedLines) {
                var hasMovement = tokensC.HasMovementCommand();
                var hasLinearMovement = tokensC.Any(tc => new []{"G1", "G01"}.Contains(tc));
                if (hasMovement && !areTokensASet && !areTokensBSet) {
                    // Some movement command, and we're at a 'start'
                    tokensA = tokensC;
                    areTokensASet = true;
                    continue;
                }

                var coordsA = tokensA.ExtractCoords();
                var coordsB = areTokensBSet ? tokensB.ExtractCoords() : new Coord();
                var coordsC = tokensC.ExtractCoords();

                if (!hasLinearMovement) {
                    // Not a linear movement command
                    if (areTokensASet)
                    {
                        yield return tokensA;
                        tokensA = new List<string>();
                        areTokensASet = false;
                    }
                    if (areTokensBSet)
                    {
                        if (inArc) {
                            tokensB = tokensB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise);

                            prevCenter = new Coord();
                            prevRadius = 0M;
                            prevIsClockwise = false;
                        }

                        yield return tokensB;
                        tokensB = new List<string>();
                        areTokensBSet = false;
                    }
                    yield return tokensC;
                    continue;
                }

                if (!tokensA.AreTokensCompatible(tokensC)) {
                    // A movement command but A -> C are not of compatible 'form'
                    if (areTokensASet)
                    {
                        yield return tokensA;
                    }
                    if (areTokensBSet) {
                        if (inArc) {
                            tokensB = tokensB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise);

                            prevCenter = new Coord();
                            prevRadius = 0M;
                            prevIsClockwise = false;
                        }

                        yield return tokensB;
                        tokensB = new List<string>();
                        areTokensBSet = false;
                    }
                    tokensA = tokensC;
                    areTokensASet = true;
                    continue;
                }

                if (!areTokensBSet) {
                    // Set up the B token
                    tokensB = tokensC;
                    areTokensBSet = true;
                    continue;
                }

                // Check we've got a full set of coords for the three token sets
                var hasCoords = (coordsA.Set & coordsB.Set & coordsC.Set) == CoordSet.All;
                var withinBounds = false;
                var isSignificant = false;

                if (hasCoords)
                {
                    // basic check that B is roughly between A and C
                    // curves just off some orthogonal plane will fail this test - but I'm OK with that
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

                    var xIsRelevant = (coordsAC.X >= tolerance && coordsAB.X >= tolerance && coordsBC.X >= tolerance) ? 1 : 0;
                    var yIsRelevant = (coordsAC.Y >= tolerance && coordsAB.Y >= tolerance && coordsBC.Y >= tolerance) ? 1 : 0;
                    var zIsRelevant = (coordsAC.Z >= tolerance && coordsAB.Z >= tolerance && coordsBC.Z >= tolerance) ? 1 : 0;

                    isSignificant = xIsRelevant + yIsRelevant + zIsRelevant < 2;

                    if (isSignificant)
                    {
                        (center, radius, isClockwise) = Utility.FindCircle(coordsA, coordsB, coordsC);

                        if (radius > tolerance)
                        {
                            var radSqr = radius.Sqr();

                            var bcDistance = (coordsB, coordsC).Distance().Sqr() / 4;
                            var bcr = (double)radius - Math.Sqrt((double)(radSqr - bcDistance));

                            if (inArc)
                            {
                                var centerDiff = Coord.Difference(center, prevCenter);
                                var radiusDiff = Math.Abs(radius - prevRadius);

                                if (centerDiff.X <= tolerance && centerDiff.Y <= tolerance && centerDiff.Z <= tolerance && radiusDiff <= tolerance)
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
                    yield return tokensA;
                    if (!inArc) {
                        tokensA = tokensB;
                        tokensB = tokensC;

                        areTokensASet = true;
                        areTokensBSet = true;
                    }
                    else
                    {
                        // Finish the arc manipulation we were doing,
                        // the new arc end point will be tokensB
                        tokensB = tokensB.ConvertLinearToArc(coordsA, prevCenter, prevIsClockwise);

                        prevCenter = new Coord();
                        prevRadius = 0M;
                        prevIsClockwise = false;

                        yield return tokensB;
                        tokensA = tokensC;
                        areTokensASet = true;
                        tokensB = new List<string>();
                        areTokensBSet = false;

                        inArc = false;
                    }
                }
                else
                {
                    // They are on an arc! so move things along, silently drop tokenB
                    prevCenter = center;
                    prevRadius = radius;
                    prevIsClockwise = isClockwise;

                    tokensB = tokensC;
                    areTokensBSet = true;

                    inArc = true;
                }
            }
        }

        private static List<string> ConvertLinearToArc(this List<string> tokensB, Coord coordsA, Coord prevCenter, bool prevIsClockwise) {
            for (var ix = 0; ix < tokensB.Count; ix++)
            {
                if (tokensB[ix] == "G1" || tokensB[ix] == "G01")
                {
                    tokensB[ix] = prevIsClockwise ? "G2" : "G3";
                    break;
                }
            }
            if ((prevCenter.Set & CoordSet.X) == CoordSet.X && (coordsA.Set & CoordSet.X) == CoordSet.X)
            {
                tokensB.Add($"I{prevCenter.X - coordsA.X:0.####}");
            }
            if ((prevCenter.Set & CoordSet.Y) == CoordSet.Y && (coordsA.Set & CoordSet.Y) == CoordSet.Y)
            {
                tokensB.Add($"J{prevCenter.Y - coordsA.Y:0.####}");
            }
            if ((prevCenter.Set & CoordSet.Z) == CoordSet.Z && (coordsA.Set & CoordSet.Z) == CoordSet.Z)
            {
                tokensB.Add($"K{prevCenter.Z - coordsA.Z:0.####}");
            }

            return tokensB;
        }


        public static async IAsyncEnumerable<List<string>> DedupSelectTokens(this IAsyncEnumerable<List<string>> tokenizedLines, List<char> selectedTokens) {
            var previousSelectedTokens = selectedTokens.Select(st => $"{st}0.00").ToList();

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsNotCommandOrArguments()) {
                    yield return tokens;
                    continue;
                }

                for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                    if (previousSelectedTokens.Any(c => c == tokens[ix])) {
                        tokens.RemoveAt(ix);
                    }
                }

                for (var ix = 0; ix < previousSelectedTokens.Count; ix++) {
                    var newToken = tokens.FirstOrDefault(t => t[0] == previousSelectedTokens[ix][0]);
                    if (newToken != null) {
                        previousSelectedTokens[ix] = newToken;
                    }
                }

                yield return tokens;
            }
        }

        public static async IAsyncEnumerable<List<string>> DedupTokens(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var previousXYZCoords = new List<string>() {"X0.00", "Y0.00", "Z0.00"};
            var previousIJKCoords = new List<string>() {"I0.00", "J0.00", "K0.00"};

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsNotCommandOrArguments()) {
                    yield return tokens;
                    continue;
                }

                for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                    if (previousXYZCoords.Any(c => c == tokens[ix])) {
                        tokens.RemoveAt(ix);
                    }
                }

                for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                    if (previousIJKCoords.Any(c => c == tokens[ix])) {
                        tokens.RemoveAt(ix);
                    }
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++) {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousXYZCoords[ix][0]);
                    if (newCoord != null) {
                        previousXYZCoords[ix] = newCoord;
                    }
                }

                for (var ix = 0; ix < previousIJKCoords.Count; ix++) {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousIJKCoords[ix][0]);
                    if (newCoord != null) {
                        previousIJKCoords[ix] = newCoord;
                    }
                }

                yield return tokens;
            }
        }
    }
}
