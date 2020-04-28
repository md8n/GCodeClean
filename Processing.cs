// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

namespace GCodeClean
{
    public static class Processing {
        public static async IAsyncEnumerable<List<string>> Tokenize(this IAsyncEnumerable<string> lines) {
            await foreach (var line in lines) {
                // prepare comments to always have spaces before and after
                var cleanedline = line.Replace("(", " (").Replace(")", ") ").Replace("  (", " (").Replace(")  ", ") ");
                var tokens = cleanedline.Split().ToList();
                for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                    tokens[ix] = tokens[ix].Trim().ToUpper();
                    if (string.IsNullOrWhiteSpace(tokens[ix])) {
                        // Remove any empty tokens
                        tokens.RemoveAt(ix);
                        continue;
                    }
                    if (tokens[ix].EndsWith(')') && !tokens[ix].StartsWith('(')) {
                        // Collapse comments into a single token
                        tokens[ix - 1] += ' ' + tokens[ix];
                        tokens.RemoveAt(ix);
                        continue;
                    }
                }

                yield return tokens;
            }
        }

        public static async IAsyncEnumerable<List<string>> Clip(this IAsyncEnumerable<List<string>> tokenizedLines) {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            await foreach (var tokens in tokenizedLines) {
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
                            // Round to 2dp for mm and 3dp for inch
                            var clip = (context["lengthUnits"] == "mm") ? 1 : 2;
                            var clipFormat = clip == 1 ? "{0}{1:0.0}" : "{0}{1:0.00}";
                            value = Math.Round(value.Value, clip);
                            tokens[ix] = String.Format(clipFormat, subToken, value);
                        }
                    }
                }

                yield return tokens;
            }
        }
        
        public static async IAsyncEnumerable<List<string>> Augment(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var previousXYZCoords = new List<string>() {"X0.00", "Y0.00", "Z0.00"};
            var previousIJKCoords = new List<string>() {"I0.00", "J0.00", "K0.00"};

            await foreach (var tokens in tokenizedLines) {
                var hasXY = false;
                var hasIJ = false;
                foreach(var token in tokens) {
                    if (token[0] == 'X' || token[0] == 'Y') {
                        hasXY = true;
                        break;
                    }
                }
                foreach(var token in tokens) {
                    if (token[0] == 'I' || token[0] == 'J') {
                        hasIJ = true;
                        break;
                    }
                }

                for (var ix = 0; ix < previousXYZCoords.Count; ix++) {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousXYZCoords[ix][0]);
                    if (newCoord == null) {
                        newCoord = previousXYZCoords[ix];
                    }
                    previousXYZCoords[ix] = newCoord;
                }

                for (var ix = 0; ix < previousIJKCoords.Count; ix++) {
                    var newCoord = tokens.FirstOrDefault(t => t[0] == previousIJKCoords[ix][0]);
                    if (newCoord == null) {
                        newCoord = previousIJKCoords[ix];
                    }
                    previousIJKCoords[ix] = newCoord;
                }

                if (hasXY) {
                    for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                        if (tokens[ix][0] == 'X' || tokens[ix][0] == 'Y' || tokens[ix][0] == 'Z') {
                            tokens.RemoveAt(ix);
                        }
                    }
                    tokens.AddRange(previousXYZCoords);
                }

                if (hasIJ) {
                    for (var ix = tokens.Count - 1; ix >= 0; ix--) {
                        if (tokens[ix][0] == 'I' || tokens[ix][0] == 'J') {
                            tokens.RemoveAt(ix);
                        }
                    }
                    tokens.AddRange(previousIJKCoords);
                }

                yield return tokens;                    
            }
        }

        public static async IAsyncEnumerable<List<string>> Dedup(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var previousTokens = new List<string>();
            await foreach (var tokens in tokenizedLines) {
                if (!previousTokens.AreTokensEqual(tokens)) {
                    previousTokens = tokens;
                    yield return tokens;
                }

                // Silently drop the duplicate
            }
        }

        /// Testing whether A -> B -> C is a straight line
        /// and eliminating B if that's the case
        public static async IAsyncEnumerable<List<string>> DedupLinear(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var tokensA = new List<string>();
            var tokensB = new List<string>();
            var areTokensBSet = false;
            await foreach (var tokensC in tokenizedLines) {
                var hasLinearMovement = tokensC.Any(tc => new []{"G0", "G1", "G00", "G01"}.Contains(tc));
                if (!hasLinearMovement || !tokensA.AreTokensCompatible(tokensC)) {
                    // Not a linear movement command or A -> C are not of compatible 'form'
                    yield return tokensA;
                    if (areTokensBSet) {
                        yield return tokensB;
                        tokensB = new List<string>();
                        areTokensBSet = false;
                    }
                    tokensA = tokensC;
                    continue;
                }

                if (!areTokensBSet) {
                    // Set up the B token
                    tokensB = tokensC;
                    areTokensBSet = true;
                    continue;
                }

                // A 'wobble' - we know that A -> B -> C cannot be colinear, because A == C
                var hasWobble = tokensA.AreTokensEqual(tokensC);
                var notCoords = false;
                var outOfBounds = false;
                var notColinear = false;

                if (!hasWobble) {
                    // Extract X, Y, Z from each set of tokens
                    var coordsA = tokensA.ExtractCoords();
                    var coordsB = tokensB.ExtractCoords();
                    var coordsC = tokensC.ExtractCoords();

                    // Check we've got a full set of coords for the three token sets
                    notCoords = (coordsA.Set + coordsB.Set + coordsC.Set).Length != 9;

                    if (!notCoords) {
                        // basic check that B is roughly between A and C
                        outOfBounds = !(coordsB.X.WithinRange(coordsA.X, coordsC.X)
                            && coordsB.Y.WithinRange(coordsA.Y, coordsC.Y)
                            && coordsB.Z.WithinRange(coordsA.Z, coordsC.Z));
                    }

                    if (!notCoords && !outOfBounds) {
                        var acX = (Double)(coordsA.X - coordsC.X);
                        var acY = (Double)(coordsA.Y - coordsC.Y);
                        var acZ = (Double)(coordsA.Z - coordsC.Z);

                        var abX = (Double)(coordsA.X - coordsB.X);
                        var abY = (Double)(coordsA.Y - coordsB.Y);
                        var abZ = (Double)(coordsA.Z - coordsB.Z);

                        var bcX = (Double)(coordsB.X - coordsC.X);
                        var bcY = (Double)(coordsB.Y - coordsC.Y);
                        var bcZ = (Double)(coordsB.Z - coordsC.Z);

                        var acXY2 = Math.Sqrt((acX * acX) + (acY * acY));
                        var abXY2 = Math.Sqrt((abX * abX) + (abY * abY));
                        var bcXY2 = Math.Sqrt((bcX * bcX) + (bcY * bcY));

                        var acXZ2 = Math.Sqrt((acX * acX) + (acZ * acZ));
                        var abXZ2 = Math.Sqrt((abX * abX) + (abZ * abZ));
                        var bcXZ2 = Math.Sqrt((bcX * bcX) + (bcZ * bcZ));

                        var xyOoM = Math.Max(acXY2, abXY2).OoM();
                        var xzOoM = Math.Max(acXZ2, abXZ2).OoM();

                        var xyTolerance = Math.Min(Math.Pow(10, xyOoM) * 0.005, 0.005);
                        var xzTolerance = Math.Min(Math.Pow(10, xzOoM) * 0.005, 0.005);

                        notColinear = !((acXY2 - (abXY2 + bcXY2) <= xyTolerance) && (acXZ2 - (abXZ2 + bcXZ2) <= xzTolerance));
                    }
                }

                yield return tokensA;
                if (hasWobble || notCoords || outOfBounds || notColinear) {
                    yield return tokensB;
                }
                // else - They are colinear! so move things along and silently drop tokenB

                tokensA = tokensC;
                tokensB = new List<string>();
                areTokensBSet = false;
            }
        }

        public static async IAsyncEnumerable<List<string>> Annotate(this IAsyncEnumerable<List<string>> tokenizedLines) {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            var previousTokenCodes = new List<string>();

            await foreach (var tokens in tokenizedLines) {
                var annotationTokens = new List<string>();
                var tokenCodes = new List<string>();
                foreach (var token in tokens) {
                    var replacement = (JObject)replacements[token];
                    if (replacement != null) {
                        foreach(var contextToken in replacement) {
                            context[contextToken.Key] = (string)contextToken.Value;
                        }
                    }

                    var annotation = (string)tokenDefs[token];
                    if (annotation == null) {
                        var subToken = "" + token[0];
                        tokenCodes.Add(subToken);
                        annotation = (string)tokenDefs[subToken];
                        context[token[0] + "value"] = (string)token.Substring(1);
                    } else {
                        tokenCodes.Add(token);
                    }
                    if (annotation != null) {
                        foreach(var contextToken in context) {
                            annotation = annotation.Replace("{" + contextToken.Key + "}", contextToken.Value);
                        }
                        annotationTokens.Add(annotation);
                    }
                }
                var isDuplicate = true;
                if (previousTokenCodes.Count != tokenCodes.Count) {
                    isDuplicate = false;
                } else {
                    for (var ix = 0; ix < tokenCodes.Count; ix++) {
                        if (previousTokenCodes[ix] != tokenCodes[ix]) {
                            isDuplicate = false;
                            break;
                        }
                    }
                }

                if (!isDuplicate && annotationTokens.Count > 0) {
                    tokens.Add($"({string.Join(", ", annotationTokens)})");
                    previousTokenCodes = tokenCodes;
                }

                yield return tokens;
            }
        }

        public static async IAsyncEnumerable<List<string>> DedupTokens(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var previousXYZCoords = new List<string>() {"X0.00", "Y0.00", "Z0.00"};
            var previousIJKCoords = new List<string>() {"I0.00", "J0.00", "K0.00"};

            await foreach (var tokens in tokenizedLines) {
                if (!tokens.Any()) {
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

        public static async IAsyncEnumerable<string> JoinTokens(this IAsyncEnumerable<List<string>> tokenizedLines) {
            var isFirstLine = true;
            await foreach (var tokens in tokenizedLines) {
                var joinedLine = string.Join(' ', tokens);
                if (String.IsNullOrWhiteSpace(joinedLine) && isFirstLine) {
                    continue;
                }
                isFirstLine = false;

                yield return joinedLine;
            }
        }

        private static Boolean AreTokensEqual(this List<string> tokensA, List<string> tokensB) {
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

        private static Boolean AreTokensCompatible(this List<string> tokensA, List<string> tokensB) {
            if (tokensA.Count != tokensB.Count) {
                return false;
            }
            var isCompatible = true;
            for (var ix = 0; ix < tokensB.Count; ix++) {
                if (tokensA[ix][0] != tokensB[ix][0]) {
                    isCompatible = false;
                    break;
                }
            }
            return isCompatible;
        }

        private static (decimal X, decimal Y, decimal Z, string Set) ExtractCoords(this List<string> tokens) {
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

        private static decimal? ExtractCoord(this string token) {
            decimal value;
            if (decimal.TryParse((string)token.Substring(1), out value)) {
                return value;
            }
            return null;
        }

        /// Is B between A and C, inclusive
        private static Boolean WithinRange(this decimal B, decimal A, decimal C) {
            var low = Math.Min(A, C);
            var high = Math.Max(A, C);

            return B >= low && B <= high;
        }

        private static int OoM(this double value) {
            var mag = 0;

            if (value == 0) {
                return mag;
            }
 
            if (Math.Abs(value) > 1.0) {
                while(value > 1) { 
                    mag++; 
                    value /= 10; 
                };
            } else if (Math.Abs(value) < 0.1) {
                while(value < 1) { 
                    mag--; 
                    value *= 10; 
                };
            }

            return mag;
        }
    }
}
