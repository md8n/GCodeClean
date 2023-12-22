// Copyright (c) 2022-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using GCodeClean.IO;
using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static partial class Workflow {
        public static async IAsyncEnumerable<string> CleanLines(
            this IAsyncEnumerable<string> inputLines,
            Context preambleContext,
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
            var processedLines = firstPhaseLines
                .PreAndPostamblePhase(preambleContext, zClamp)
                .CleanLinesSecondPhase(eliminateNeedlessTravel, zClamp, arcTolerance, tolerance)
                .CleanLinesThirdPhase(dedupSelection, annotate, tokenDefinitions);
            var reassembledLines = processedLines.ReassembleLines(minimisationStrategy);

            await foreach (var line in reassembledLines) {   
                yield return line;
            }
        }

        public static async Task<Context> GetPreambleContext(this string inputFilename) {
            // Determine our starting context
            var preambleSourceLines = inputFilename.ReadLinesAsync();
            var preambleContextUnclean = await preambleSourceLines.TokeniseToLine(ModalGroup.ModalAllMotion).BuildPreamble();
            var preambleContextCleanLines = preambleContextUnclean.Lines.Select(cl => cl.line).ToAsyncEnumerable()
                .DedupRepeatedTokens()
                .Augment()
                .SingleCommandPerLine()
                .DedupContext();
            var preambleContext = await preambleContextCleanLines.BuildPreamble();

            return preambleContext;
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
        /// <param name="inputLines"></param>
        /// <param name="eliminateNeedlessTravel"></param>
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
