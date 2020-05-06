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
                    if (!tokens.IsEmptyOrComments()) {
                        previousTokens = tokens;
                    }

                    yield return tokens;
                }

                // Silently drop the duplicate
            }
        }

        /// Testing whether A -> B -> C is a straight line
        /// and eliminating B if that's the case
        public static async IAsyncEnumerable<List<string>> DedupLinear(this IAsyncEnumerable<List<string>> tokenizedLines, Double tolerance) {
            var tokensA = new List<string>();
            var tokensB = new List<string>();
            var areTokensASet = false;
            var areTokensBSet = false;

            await foreach (var tokensC in tokenizedLines) {
                var hasLinearMovement = tokensC.Any(tc => new []{"G0", "G1", "G00", "G01"}.Contains(tc));
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
                var notCoords = (coordsA.Set + coordsB.Set + coordsC.Set).Length != 9;
                var outOfBounds = false;
                var notSignificant = false;

                if (!notCoords)
                {
                    // basic check that B is roughly between A and C
                    outOfBounds = !(coordsB.X.WithinRange(coordsA.X, coordsC.X)
                        && coordsB.Y.WithinRange(coordsA.Y, coordsC.Y)
                        && coordsB.Z.WithinRange(coordsA.Z, coordsC.Z));                    
                }

                if (!notCoords && !outOfBounds)
                {
                    // Check if any value is too small to matter                    
                    var (acX, acY, acZ) = coordsA.CoordsDifference(coordsC);
                    var (abX, abY, abZ) = coordsA.CoordsDifference(coordsB);
                    var (bcX, bcY, bcZ) = coordsB.CoordsDifference(coordsC);

                    var xIsRelevant = (acX >= tolerance && abX >= tolerance && bcX >= tolerance) ? 1 : 0;
                    var yIsRelevant = (acY >= tolerance && abY >= tolerance && bcY >= tolerance) ? 1 : 0;
                    var zIsRelevant = (acZ >= tolerance && abZ >= tolerance && bcZ >= tolerance) ? 1 : 0;

                    notSignificant = xIsRelevant + yIsRelevant + zIsRelevant < 2;

                    var xyNotRelevant = true;
                    var xzNotRelevant = true;
                    var yzNotRelevant = true;

                    if (!notSignificant) {
                        var acXYAngle = (acX, acY).Angle();
                        var abXYAngle = (abX, abY).Angle();

                        var acXZAngle = (acX, acZ).Angle();
                        var abXZAngle = (abX, abZ).Angle();

                        var acYZAngle = (acY, acZ).Angle();
                        var abYZAngle = (abY, abZ).Angle();

                        if (xIsRelevant + yIsRelevant == 2)
                        {
                            xyNotRelevant = Math.Abs(acXYAngle - abXYAngle) < tolerance;
                        }
                        if (xIsRelevant + zIsRelevant == 2)
                        {
                            xzNotRelevant = Math.Abs(acXZAngle - abXZAngle) < tolerance;
                        }
                        if (yIsRelevant + zIsRelevant == 2)
                        {
                            yzNotRelevant = Math.Abs(acYZAngle - abYZAngle) < tolerance;
                        }
                    }

                    notSignificant = (xyNotRelevant && xzNotRelevant && yzNotRelevant);
                }

                yield return tokensA;
                if (notCoords || outOfBounds || notSignificant) {
                    yield return tokensB;
                }
                // else - They are colinear! so move things along and silently drop tokenB

                tokensA = tokensC;
                tokensB = new List<string>();
                areTokensBSet = false;
            }
        }

        public static async IAsyncEnumerable<List<string>> DedupSelectTokens(this IAsyncEnumerable<List<string>> tokenizedLines, List<char> selectedTokens) {
            var previousSelectedTokens = selectedTokens.Select(st => $"{st}0.00").ToList();

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsEmptyOrComments()) {
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
                if (tokens.IsEmptyOrComments()) {
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
