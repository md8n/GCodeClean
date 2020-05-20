// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GCodeClean.Processing
{
    public static class Tokenizer {
        /// <summary>
        /// A 'basic' gcode parser pattern, this does not support expressions that equate to numbers
        /// </summary>
        public const string pattern = @"((\%)|((?<linenumber>N\s*\d{1,5})?\s*(?<word>[ABCDFGHIJKLMNPRSTXYZ]\s*[+-]?(\d|\s)*\.?(\d|\s)*\s*)|(?<comment>\(.*?\)\s*)))";

        public static async IAsyncEnumerable<List<string>> Tokenize(this IAsyncEnumerable<string> lines) {
            await foreach (var line in lines) {
                yield return line.Tokenize();
            }
        }

        public static List<string> Tokenize(this string line) {
            var tokens = new List<string>();

            foreach (Match match in Regex.Matches(line, pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
            {
                var token = match.Value.Trim();
                // if this isn't a comment or file terminator then strip out all spaces
                if (!token.StartsWith('(') && !token.StartsWith('%')) {
                    token.Replace(" ", "");
                    decimal number;
                    if (token.Length == 1 || !decimal.TryParse(token.Substring(1), out number)) {
                        // Invalid command or argument - doesn't have a valid number
                        continue;
                    }
                    token = token.ToUpperInvariant();
                }
                tokens.Add(token);
            }

            return tokens;
        }

        public static async IAsyncEnumerable<string> JoinTokens(this IAsyncEnumerable<List<string>> tokenizedLines, string minimisationStrategy) {
            var isFirstLine = true;
            var prevLine = "";
            var joiner = minimisationStrategy == "HARD" ? "" : " ";
            await foreach (var tokens in tokenizedLines) {
                var joinedLine = string.Join(joiner, tokens);
                if (string.IsNullOrWhiteSpace(joinedLine) && isFirstLine) {
                    continue;
                }
                isFirstLine = false;

                if (!(string.IsNullOrWhiteSpace(prevLine) && string.IsNullOrWhiteSpace(joinedLine)))
                {
                    yield return joinedLine;
                }

                prevLine = joinedLine;
            }
        }
    }
}
