// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Processing
{
    public static class Utility {
        public static Boolean IsEmptyOrComments(this List<string> tokens) {
            return tokens.Count == 0 || tokens.All(t => t[0] == '(');
        }

        public static Boolean AreTokensEqual(this List<string> tokensA, List<string> tokensB) {
            if (tokensA.Count != tokensB.Count) {
                return false;
            }
            var isDuplicate = true;
            for (var ix = 0; ix < tokensB.Count; ix++) {
                if (tokensA[ix] != tokensB[ix]) {
                    isDuplicate = false;
                    break;
                }
            }
            return isDuplicate;
        }

        public static Boolean AreTokensCompatible(this List<string> tokensA, List<string> tokensB) {
            if (tokensA.Count != tokensB.Count) {
                return false;
            }
            var isCompatible = true;
            for (var ix = 0; ix < tokensB.Count; ix++) {
                if (tokensA[ix][0] != tokensB[ix][0]) {
                    isCompatible = false;
                    break;
                }
                if (tokensA[ix][0] == 'G' || tokensA[ix][0] == 'M') {
                    // For 'Commands' the whole thing must be the same
                    if (tokensA[ix] != tokensB[ix]) {
                        isCompatible = false;
                        break;
                    }
                }
            }
            return isCompatible;
        }

        public static (decimal X, decimal Y, decimal Z, string Set) ExtractCoords(this List<string> tokens) {
            (decimal X, decimal Y, decimal Z, string Set) coords = (0M, 0M, 0M, "");
            decimal? value = null;
            foreach(var token in tokens) {
                value = token.ExtractCoord();
                if (value.HasValue) {
                    if (token[0] == 'X') {
                        coords.X = value.Value;
                        coords.Set += "X";
                    }                    
                    if (token[0] == 'Y') {
                        coords.Y = value.Value;
                        coords.Set += "Y";
                    }                    
                    if (token[0] == 'Z') {
                        coords.Z = value.Value;
                        coords.Set += "Z";
                    }                    
                }
            }

            return coords;
        }

        public static decimal? ExtractCoord(this string token) {
            decimal value;
            if (decimal.TryParse((string)token.Substring(1), out value)) {
                return value;
            }
            return null;
        }

        /// Is B between A and C, inclusive
        public static Boolean WithinRange(this decimal B, decimal A, decimal C) {
            var low = Math.Min(A, C);
            var high = Math.Max(A, C);

            return B >= low && B <= high;
        }

        public static Double Angle(this Double da, Double db) {
            var theta = Math.Atan2((Double)da, (Double)db); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return theta;
        }

        public static Double Angle(this (Double A, Double B) d) {
            var theta = Math.Atan2((Double)d.A, (Double)d.B); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return theta;
        }

        public static (Double X, Double Y, Double Z) CoordsDifference(this (decimal X, decimal Y, decimal Z, string Set) coords1, (decimal X, decimal Y, decimal Z, string Set) coords2) {
            return ((Double)(coords2.X - coords1.X), (Double)(coords2.Y - coords1.Y), (Double)(coords2.Z - coords1.Z));
        }
    }
}
