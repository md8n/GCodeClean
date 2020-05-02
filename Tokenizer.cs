// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeClean
{
    public static class Tokenizer {
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
    }
}
