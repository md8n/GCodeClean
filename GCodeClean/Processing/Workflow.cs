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
            var preambleContext = await firstPhaseLines.BuildPreamble(Default.Preamble());
            var secondPhaseLines = firstPhaseLines.ProcessLinesSecondPhase(preambleContext, zClamp, arcTolerance, tolerance);
            var thirdPhaseLines = secondPhaseLines.ProcessLinesThirdPhase(dedupSelection, minimisationStrategy, annotate, tokenDefinitions);

            await foreach (var line in thirdPhaseLines) {
                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> ProcessLinesFirstPhase(
            this IAsyncEnumerable<string> inputLines,
            bool lineNumbers
        ) {
            var tokenisedLines = inputLines.TokeniseToLine();
            var firstPhaseLines = (lineNumbers ? tokenisedLines : tokenisedLines.EliminateLineNumbers())
                .DedupRepeatedTokens()
                .Augment()
                .SingleCommandPerLine();

            await foreach (var line in firstPhaseLines) {
                yield return line;
            }
        }

        public static async IAsyncEnumerable<Line> ProcessLinesSecondPhase(
            this IAsyncEnumerable<Line> firstPhaseLines,
            Context preambleContext,
            decimal zClamp,
            decimal arcTolerance,
            decimal tolerance
        ) {
            var lengthUnits = Utility.GetLengthUnits(preambleContext);
            zClamp = Utility.ConstrictZClamp(lengthUnits, zClamp);
            var coordPlane = preambleContext.GetModalState(ModalGroup.ModalPlane).ToString();

            var secondPhaseLines = firstPhaseLines
                .FileDemarcation(zClamp)
                .InjectPreamble(preambleContext, zClamp)
                .ZClamp(zClamp)
                .ConvertArcRadiusToCenter(coordPlane)
                .DedupLine()
                .SimplifyShortArcs(lengthUnits, arcTolerance)
                .DedupLinearToArc(lengthUnits, coordPlane, tolerance)
                .Clip(lengthUnits, tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(tolerance);

            await foreach (var line in secondPhaseLines) {
                yield return line;
            }
        }

        public static async IAsyncEnumerable<string> ProcessLinesThirdPhase(
            this IAsyncEnumerable<Line> secondPhaseLines,
            List<char> dedupSelection,
            string minimisationStrategy,
            bool annotate,
            JsonDocument tokenDefinitions
        ) {
            var minimisedLines = secondPhaseLines.DedupSelectTokens(dedupSelection);
            var annotatedLines = annotate ? minimisedLines.Annotate(tokenDefinitions.RootElement) : minimisedLines;
            var reassembledLines = annotatedLines.JoinLines(minimisationStrategy);

            await foreach (var line in reassembledLines) {
                yield return line;
            }
        }
    }
}
