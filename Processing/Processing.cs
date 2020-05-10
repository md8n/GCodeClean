// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace GCodeClean.Processing
{
    public static class Processing {
        public static async IAsyncEnumerable<List<string>> Clip(this IAsyncEnumerable<List<string>> tokenizedLines) {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsNotCommandOrArguments()) {
                    yield return tokens;
                    continue;
                }

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
                            // Round to 3dp for mm and 4dp for inch
                            var clip = (context["lengthUnits"] == "mm") ? 3 : 4;
                            var clipFormat = clip == 1 ? "{0}{1:0.000}" : "{0}{1:0.0000}";
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
            var previousIJKCoords = new List<string>() {"I0.00", "J0.00"};

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsNotCommandOrArguments()) {
                    yield return tokens;
                    continue;
                }

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
                foreach(var token in tokens) {
                    if (token[0] == 'K') {
                        previousIJKCoords.Add("K0.00");
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
                        if (tokens[ix][0] == 'I' || tokens[ix][0] == 'J' || tokens[ix][0] == 'K') {
                            tokens.RemoveAt(ix);
                        }
                    }
                    tokens.AddRange(previousIJKCoords);
                }

                yield return tokens;                    
            }
        }

        public static async IAsyncEnumerable<List<string>> Annotate(this IAsyncEnumerable<List<string>> tokenizedLines) {
            JObject tokenDefinitions = JObject.Parse(File.ReadAllText("tokenDefinitions.json"));

            var replacements = tokenDefinitions["replacements"];
            var tokenDefs = tokenDefinitions["tokenDefs"];
            var context = new Dictionary<string, string>();

            var previousTokenCodes = new List<string>();

            await foreach (var tokens in tokenizedLines) {
                if (tokens.IsNotCommandOrArguments()) {
                    yield return tokens;
                    continue;
                }

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
    }
}
