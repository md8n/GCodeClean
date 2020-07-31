// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Text.RegularExpressions;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Tokeniser {
        /// <summary>
        /// A 'basic' GCode parser pattern, this does not support expressions that equate to numbers
        /// </summary>
        private const string Pattern = @"((\%)|((?<linenumber>N\s*\d{1,5})?\s*(?<word>[A-Z]\s*[+-]?(\d|\s)*\.?(\d|\s)*\s*)|(?<comment>\(.*?\)\s*)|(?<fullcomment>\;.*$)))";

        public static async IAsyncEnumerable<Line> TokeniseToLine(this IAsyncEnumerable<string> lines) {
            await foreach (var line in lines) {
                yield return new Line(line);
            }
        }

        public static async IAsyncEnumerable<Line> EliminateLineNumbers(this IAsyncEnumerable<Line> tokenisedLines) {
            await foreach (var line in tokenisedLines) {
                line.RemoveTokens(new List<char>{'N'});
                yield return line;
            }
        }

        public static List<string> Tokenise(this string line) {
            var tokens = new List<string>();
            var matches = Regex.Matches(line, Pattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

            foreach (Match match in matches)
            {
                var groupCtr = 0;
                foreach(Group group in match.Groups) {
                    if (group.Name == groupCtr.ToString() || !group.Success) {
                        continue;
                    }
                    groupCtr++;

                    var token = group.Value.Trim();
                    // if this isn't a comment or file terminator then strip out all spaces
                    if (!token.StartsWith('(') && !token.StartsWith('%') && !token.StartsWith(';')) {
                        token = token.Replace(" ", "");
                        if (token.Length == 1 || !decimal.TryParse(token.Substring(1), out var _))
                        {
                            // Invalid command or argument - doesn't have a valid number
                            continue;
                        }
                        token = token.ToUpperInvariant();
                    }
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        public static async IAsyncEnumerable<string> JoinLines(this IAsyncEnumerable<Line> tokenisedLines, string minimisationStrategy) {
            var isFirstLine = true;
            var prevLine = "";
            var joiner = minimisationStrategy == "HARD" ? "" : " ";
            await foreach (var line in tokenisedLines) {
                var joinedLine = string.Join(joiner, line.AllTokens);
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
