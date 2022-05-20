// Copyright (c) 2022-22 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

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
        )
        {
            var preambleContext = Default.Preamble();

            var tokenisedLines = inputLines.TokeniseToLine();
            var outputLines = (lineNumbers ? tokenisedLines : tokenisedLines.EliminateLineNumbers())
                .DedupRepeatedTokens()
                .Augment()
                .SingleCommandPerLine()
                .FileDemarcation(preambleContext, zClamp)
                .InjectPreamble(preambleContext, zClamp)
                .ZClamp(preambleContext, zClamp)
                .ConvertArcRadiusToCenter(preambleContext)
                .DedupLine()
                .SimplifyShortArcs(preambleContext, arcTolerance)
                .DedupLinearToArc(preambleContext, tolerance)
                .Clip(preambleContext, tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(tolerance)
                ;

            var minimisedLines = outputLines.DedupSelectTokens(dedupSelection);

            var annotatedLines = annotate ? minimisedLines.Annotate(tokenDefinitions.RootElement) : minimisedLines;
            var reassembledLines = annotatedLines.JoinLines(minimisationStrategy);

            await foreach (var line in reassembledLines)
            {
                yield return line;
            }
        }
    }
}
