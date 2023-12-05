// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static partial class Tokeniser {
        /// <summary>
        /// A GCode parser pattern for full line statements only (file terminators and full line comments)
        /// </summary>
        /// <remarks>This supports the illegal comment form that uses a semi-colon.</remarks>
        [GeneratedRegex("(?<fileterminator>^\\%$)|(?<fullcomment>^\\(.*?\\)$)|(?<badfullcomment>^\\;.*$)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "en-AU")]
        private static partial Regex RegexFullLinePattern();

        /// <summary>
        /// A GCode parser pattern for line numbers only
        /// </summary>
        [GeneratedRegex("(?<linenumber>N\\s*\\d{1,5})", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "en-AU")]
        private static partial Regex RegexLineNumberPattern();

        [GeneratedRegex("(?<linenumber>N\\s*\\d{1,5})")]
        private static partial Regex RegexLineNumberReplace();

        /// <summary>
        /// A GCode parser for comment statements only
        /// </summary>
        /// <remarks>This supports the illegal comment form that uses a semi-colon.</remarks>
        [GeneratedRegex("(?<comment>\\(.*?\\))|(?<badcomment>\\;.*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "en-AU")]
        private static partial Regex RegexCommentPattern();

        [GeneratedRegex("(?<comment>\\(.*?\\))|(?<badcomment>\\;.*)")]
        private static partial Regex RegexCommentSubstitute();

        [GeneratedRegex("\\s*")]
        private static partial Regex RegexWhitespace();

        /// <summary>
        /// A 'basic' GCode parser pattern,
        /// this expects all whitespace to have been removed and all comments replaced with "|||",
        /// this does not support expressions that equate to numbers
        /// </summary>
        [GeneratedRegex("(?<word>((([A-Z]|(#+\\d{1,4}=))[+-]?)((#+\\d{1,4})|(\\d*\\.?\\d*)))|(\\|\\|\\|))", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, "en-AU")]
        private static partial Regex RegexWordPattern();

        // ((\%)|((?<linenumber>N\s*\d{1,5})?\s*(?:(?<word>[A-Z]\s*[+-]?(\d|\s)*\.?(\d|\s)*\s*)|(?<comment>\(.*?\)\s*))*|(?<fullcomment>\;.*$)))

        public static async IAsyncEnumerable<Line> TokeniseToLine(this IAsyncEnumerable<string> lines, IEnumerable<Token> exitTokens = null) {
            if (exitTokens == null) {
                await foreach (var line in lines) {
                    yield return new Line(line);
                }
            } else {
                await foreach (var line in lines) {
                    var tokenisedLine = new Line(line);
                    yield return tokenisedLine;

                    if (tokenisedLine.HasTokens(exitTokens)) {
                        break;
                    }
                }
            }
        }

        public static async IAsyncEnumerable<Line> EliminateLineNumbers(this IAsyncEnumerable<Line> tokenisedLines) {
            await foreach (var line in tokenisedLines) {
                line.RemoveToken('N');
                yield return line;
            }
        }

        public static List<string> Tokenise(this string line) {
            var tokens = new List<string>();

            var matches = RegexFullLinePattern().Matches(line);
            if (matches.Count > 0) {
                foreach (Match match in matches.Cast<Match>()) {
                    var groupCtr = 0;
                    foreach (Group group in match.Groups.Cast<Group>()) {
                        if (group.Name == groupCtr.ToString() || !group.Success) {
                            continue;
                        }
                        groupCtr++;

                        var token = group.Value.Trim();

                        tokens.Add(token);
                    }
                }

                // Found a full line match so exit with that single token
                return tokens;
            }

            matches = RegexLineNumberPattern().Matches(line);
            if (matches.Count > 0) {
                foreach (Match match in matches.Cast<Match>()) {
                    var groupCtr = 0;
                    foreach (Group group in match.Groups.Cast<Group>()) {
                        if (group.Name == groupCtr.ToString() || !group.Success) {
                            continue;
                        }
                        groupCtr++;

                        var token = group.Value.Trim();

                        tokens.Add(token);
                    }
                }

                line = RegexLineNumberReplace().Replace(line, "");
            }

            var commentTokens = new List<string>();
            matches = RegexCommentPattern().Matches(line);
            if (matches.Count > 0) {
                foreach (Match match in matches.Cast<Match>()) {
                    var groupCtr = 0;
                    foreach (Group group in match.Groups.Cast<Group>()) {
                        if (group.Name == groupCtr.ToString() || !group.Success) {
                            continue;
                        }
                        groupCtr++;

                        var token = group.Value.Trim();

                        commentTokens.Add(token);
                    }
                }

                line = RegexCommentSubstitute().Replace(line, "|||");
            }

            // Eliminate all whitespace
            line = RegexWhitespace().Replace(line, "");
            // Identify all of the Tokens
            matches = RegexWordPattern().Matches(line);
            var commentCounter = 0;

            foreach (Match match in matches.Cast<Match>()) {
                var groupCtr = 0;
                foreach(Group group in match.Groups.Cast<Group>()) {
                    if (group.Name == groupCtr.ToString() || !group.Success) {
                        continue;
                    }
                    groupCtr++;

                    var token = group.Value.Trim();
                    if (token != "|||") {
                        if (token.Length == 1) {
                            // Invalid command, argument or parameter setting
                            // this token will be dumped
                            continue;
                        }
                        if (Array.Exists(Token.Parameters, p => p == token[0])) {
                            // Parameter setting requires special check
                            var parameterParts = token[1..].Split('=', StringSplitOptions.RemoveEmptyEntries);

                            if (parameterParts.Length != 2 || !int.TryParse(parameterParts[0], out var _) || !decimal.TryParse(parameterParts[1], out _)) {
                                // Not enough parts for parameter setting - or the parameter id or number are not valid
                                // this token will be dumped
                                continue;
                            }
                        } else {
                            var usesParameter = Array.Exists(Token.Parameters, p => p == token[1]) ? 2 : 1;
                            if (!decimal.TryParse(token[usesParameter..], out var _)) {
                                // Invalid command or argument - doesn't have a valid number
                                // this token will be dumped
                                continue;
                            }
                        }
                        token = token.ToUpperInvariant();
                    } else {
                        // Substitute the original comments back into their original places
                        token = commentTokens[commentCounter++];
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

                if (!(string.IsNullOrWhiteSpace(prevLine) && string.IsNullOrWhiteSpace(joinedLine))) {
                    yield return joinedLine;
                }

                prevLine = joinedLine;
            }
        }
    }
}
