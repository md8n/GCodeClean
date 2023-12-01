// Copyright (c) 2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

using GCodeClean.Structure;

using Spectre.Console;

namespace GCodeClean.Processing
{
    public static partial class Workflow {
        /// <summary>
        /// Finds GCodeClean's special 'Travelling' comments
        /// </summary>
        [GeneratedRegex("\\(\\|{2}Travelling\\|{2}\\d+\\|{2}>>G\\d+.*>>G\\d+.*>>\\|{2}\\)$")]
        private static partial Regex RegexTravellingPattern();

        public static async IAsyncEnumerable<string> CleanLines(
            this IAsyncEnumerable<string> inputLines,
            List<char> dedupSelection,
            string minimisationStrategy,
            bool lineNumbers,
            bool eliminateNeedlessTravel,
            decimal zClamp,
            decimal arcTolerance,
            decimal tolerance,
            bool annotate,
            JsonDocument tokenDefinitions
        ) {
            var firstPhaseLines = inputLines.CleanLinesFirstPhase(lineNumbers);
            // Determine our starting context
            var preambleContext = await firstPhaseLines.BuildPreamble();
 
            var processedLines = firstPhaseLines
                .PreAndPostamblePhase(preambleContext, zClamp)
                .CleanLinesSecondPhase(eliminateNeedlessTravel, zClamp, arcTolerance, tolerance)
                .CleanLinesThirdPhase(dedupSelection, annotate, tokenDefinitions);

            var reassembledLines = processedLines.ReassembleLines(minimisationStrategy);

            await foreach (var line in reassembledLines) {   
                yield return line;
            }
        }

        /// <summary>
        /// Get the GCode into a consistent state to simplify further processing
        /// </summary>
        /// <param name="inputLines"></param>
        /// <param name="lineNumbers"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> CleanLinesFirstPhase(
            this IAsyncEnumerable<string> inputLines,
            bool lineNumbers
        ) {
            var tokenisedLines = inputLines.TokeniseToLine();
            var firstPhaseLines = (lineNumbers ? tokenisedLines : tokenisedLines.EliminateLineNumbers())
                .DedupRepeatedTokens()
                .Augment()
                .SingleCommandPerLine()
                .DedupContext();

            await foreach (var line in firstPhaseLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Inject the pre and postamble GCode lines to complete getting the GCode to a consistent state
        /// </summary>
        /// <param name="inputLines"></param>
        /// <param name="preambleContext"></param>
        /// <param name="zClamp"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> PreAndPostamblePhase(
            this IAsyncEnumerable<Line> inputLines,
            Context preambleContext,
            decimal zClamp
        ) {
            var preAndPostamblePhaseLines = inputLines
                .FileDemarcation(zClamp)
                .InjectPreamble(preambleContext, zClamp);

            await foreach (var line in preAndPostamblePhaseLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Do the actual cleaning of the GCode
        /// </summary>
        /// <param name="preAndPostamblePhaseLines"></param>
        /// <param name="zClamp"></param>
        /// <param name="arcTolerance"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> CleanLinesSecondPhase(
            this IAsyncEnumerable<Line> inputLines,
            bool eliminateNeedlessTravel,
            decimal zClamp,
            decimal arcTolerance,
            decimal tolerance
        ) {
            var zClampedLines = inputLines
                .ZClamp(zClamp);
            var dedupTravellingLines = eliminateNeedlessTravel
                ? zClampedLines.DedupTravelling()
                : zClampedLines;
            var secondPhaseLines = dedupTravellingLines
                .ConvertArcRadiusToCenter()
                .DedupLine()
                .SimplifyShortArcs(arcTolerance)
                .DedupLinearToArc(tolerance)
                .Clip(tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DetectTravelling()
                .DedupLinear(tolerance);

            await foreach (var line in secondPhaseLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Do the final cleaning of the GCode and output as simple strings
        /// </summary>
        /// <param name="inputLines"></param>
        /// <param name="dedupSelection"></param>
        /// <param name="annotate"></param>
        /// <param name="tokenDefinitions"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> CleanLinesThirdPhase(
            this IAsyncEnumerable<Line> inputLines,
            List<char> dedupSelection,
            bool annotate,
            JsonDocument tokenDefinitions
        ) {
            var minimisedLines = inputLines.DedupSelectTokens(dedupSelection);
            var annotatedLines = annotate ? minimisedLines.Annotate(tokenDefinitions.RootElement) : minimisedLines;

            await foreach (var line in annotatedLines) {
                yield return line;
            }
        }

        public static async IAsyncEnumerable<(int id, Line start, Line end)> SplitFile(this IAsyncEnumerable<string> inputLines) {
            // Scan through the file for 'travelling' comments and build a table out of them
            var cuttingPaths = inputLines.SplitFileFirstPhase();

            // Take the first enry in the table, and use that to figure out the complete preamble for all extracted files

            // Take the last entry in the table, and use that to figure out the complete postamble for all extracted files

            // Process the table, for each entry, build an individual file of preamble, cutting actions, and postamble

            // This is just a dummy loop for now
            await foreach (var line in cuttingPaths) {
                yield return line;
            }
        }

        public static async IAsyncEnumerable<(int id, Line start, Line end)> SplitFileFirstPhase(this IAsyncEnumerable<string> inputLines) {
            // Scan through the file for 'travelling' comments and build a table out of them
            // if there are none, return with a relevant error message
            await foreach (var line in inputLines) {
                var match = RegexTravellingPattern().Match(line);
                if (!match.Success) {
                    continue;
                }

                var travelling = match.Value;
                var tDetails = travelling.Replace("(||Travelling||", "").Replace("||)", "").Split("||");
                var tId = Convert.ToInt32(tDetails[0]);
                var tSE = tDetails[1].Split(">>", StringSplitOptions.RemoveEmptyEntries);
                var lStart = new Line(tSE[0]);
                var lEnd = new Line(tSE[1]);
                AnsiConsole.MarkupLine($"Output lines: [bold yellow]{tId}: start '{lStart}' end '{lEnd}'[/]");

                yield return (tId, lStart, lEnd);
            }
        }

        /// <summary>
        /// Do the final processing of the GCode and output as simple strings
        /// </summary>
        /// <param name="inputLines"></param>
        /// <param name="minimisationStrategy"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<string> ReassembleLines(
            this IAsyncEnumerable<Line> inputLines,
            string minimisationStrategy
        ) {
            var reassembledLines = inputLines.JoinLines(minimisationStrategy);

            await foreach (var line in reassembledLines) {
                yield return line;
            }
        }
    }
}
