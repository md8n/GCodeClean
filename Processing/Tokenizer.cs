// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GCodeClean.Processing
{
    public static class Tokenizer {
        // A 'basic' gcode parser pattern, this does not support expressions that equate to numbers
        public const string pattern = @"((\%)|((?<linenumber>N\s*\d{1,5})?\s*(?<word>[ABCDFGHIJKLMNPRSTXYZ]\s*[+-]?\d*\.?\d*\s*)|(?<comment>\(.*\)\s*)))";

        public static async IAsyncEnumerable<List<string>> Tokenize(this IAsyncEnumerable<string> lines) {
            await foreach (var line in lines) {
                var tokens = new List<string>();
                foreach (Match match in Regex.Matches(line, pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
                {
                    tokens.Add(match.Value.Trim());
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
