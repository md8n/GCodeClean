// Copyright (c) 2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using GCodeClean.Structure;
using System.Collections.Generic;
using System.Text.Json;


namespace GCodeClean.Processing
{
    public static class Workflow
    {
        public static async IAsyncEnumerable<string> ProcessLines(
            this IAsyncEnumerable<string> inputLines,
            List<char> dedupSelection,
            string minimisationStrategy,
            bool lineNumbers,
            decimal zClamp,
            decimal arcTolerance,
            decimal tolerance,
            bool annotate,
            JsonDocument tokenDefinitions
        ) {
            var firstPhaseLines = inputLines.ProcessLinesFirstPhase(lineNumbers);
            // Determine our starting context
            var preambleContext = await firstPhaseLines.BuildPreamble();
 
            var processedLines = firstPhaseLines
                .PreAndPostamblePhase(preambleContext, zClamp)
                .ProcessLinesSecondPhase(zClamp, arcTolerance, tolerance)
                .ProcessLinesThirdPhase(dedupSelection, annotate, tokenDefinitions);

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
        public static async IAsyncEnumerable<Line> ProcessLinesFirstPhase(
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
        /// <param name="firstPhaseLines"></param>
        /// <param name="preambleContext"></param>
        /// <param name="zClamp"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> PreAndPostamblePhase(
            this IAsyncEnumerable<Line> firstPhaseLines,
            Context preambleContext,
            decimal zClamp
        ) {
            var preAndPostamblePhaseLines = firstPhaseLines
                .FileDemarcation(zClamp)
                .InjectPreamble(preambleContext, zClamp);

            await foreach (var line in preAndPostamblePhaseLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Do the actual processing of the GCode
        /// </summary>
        /// <param name="preAndPostamblePhaseLines"></param>
        /// <param name="zClamp"></param>
        /// <param name="arcTolerance"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> ProcessLinesSecondPhase(
            this IAsyncEnumerable<Line> preAndPostamblePhaseLines,
            decimal zClamp,
            decimal arcTolerance,
            decimal tolerance
        ) {
            var secondPhaseLines = preAndPostamblePhaseLines
                .ZClamp(zClamp)
                //.DedupTravelling()
                .ConvertArcRadiusToCenter()
                .DedupLine()
                .SimplifyShortArcs(arcTolerance)
                .DedupLinearToArc(tolerance)
                .Clip(tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(tolerance);

            await foreach (var line in secondPhaseLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Do the final processing of the GCode and output as simple strings
        /// </summary>
        /// <param name="secondPhaseLines"></param>
        /// <param name="dedupSelection"></param>
        /// <param name="annotate"></param>
        /// <param name="tokenDefinitions"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<Line> ProcessLinesThirdPhase(
            this IAsyncEnumerable<Line> secondPhaseLines,
            List<char> dedupSelection,
            bool annotate,
            JsonDocument tokenDefinitions
        ) {
            var minimisedLines = secondPhaseLines.DedupSelectTokens(dedupSelection);
            var annotatedLines = annotate ? minimisedLines.Annotate(tokenDefinitions.RootElement) : minimisedLines;

            await foreach (var line in annotatedLines) {
                yield return line;
            }
        }

        /// <summary>
        /// Do the final processing of the GCode and output as simple strings
        /// </summary>
        /// <param name="minimisationStrategy"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<string> ReassembleLines(
            this IAsyncEnumerable<Line> thirdPhaseLines,
            string minimisationStrategy
        ) {
            var reassembledLines = thirdPhaseLines.JoinLines(minimisationStrategy);

            await foreach (var line in reassembledLines) {
                yield return line;
            }
        }
    }
}