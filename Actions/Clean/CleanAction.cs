// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Text.Json;

using GCodeClean.IO;
using GCodeClean.Processing;
using GCodeClean.Structure;


namespace Actions.Clean;

public static class CleanAction {
    private static (string, List<char>) GetMinimisationStrategy(string minimise, List<char> dedupSelection) {
        var minimisationStrategy = string.IsNullOrWhiteSpace(minimise)
            ? "SOFT"
            : minimise.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(minimise) && minimisationStrategy != "SOFT") {
            List<char> hardList = [
                'A',
                'B',
                'C',
                'D',
                Letter.feedRate,
                Letter.gCommand,
                'H',
                'L',
                Letter.mCommand,
                Letter.lineNumber,
                'P',
                'R',
                Letter.spindleSpeed,
                Letter.selectTool,
                'X',
                'Y',
                'Z'
            ];
            dedupSelection = minimisationStrategy == "HARD" || minimisationStrategy == "MEDIUM"
                ? hardList
                : new List<char>(minimisationStrategy).Intersect(hardList).ToList();
        }

        return (minimisationStrategy, dedupSelection);
    }

    private static string DetermineOutputFilename(this string inputFile) {
        var outputFile = inputFile;

        var inputExtension = Path.GetExtension(inputFile);
        if (string.IsNullOrEmpty(inputExtension)) {
            outputFile += "-gcc.nc";
        } else {
            outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension, StringComparison.InvariantCultureIgnoreCase);
        }

        return outputFile;
    }

    public static async IAsyncEnumerable<string> ExecuteAsync(
        FileInfo filename,
        bool annotate,
        bool lineNumbers,
        string minimise,
        decimal tolerance,
        decimal arcTolerance,
        decimal zClamp,
        JsonDocument tokenDefinitions
    ) {
        var inputFile = filename.ToString();

        var (minimisationStrategy, dedupSelection) = GetMinimisationStrategy(minimise, [Letter.feedRate, 'Z']);

        var outputFile = inputFile.DetermineOutputFilename();
        yield return "";
        yield return $"Outputting to: {outputFile}";

        var eliminateNeedlessTravelling = false; // settings.EliminateNeedlessTravelling

        // Determine our starting context
        var preambleContext = await inputFile.GetPreambleContext();
        yield return "Preamble context determined";

        var inputLines = inputFile.ReadLinesAsync();
        var reassembledLines = inputLines.CleanLines(preambleContext, dedupSelection, minimisationStrategy, lineNumbers, eliminateNeedlessTravelling, zClamp, arcTolerance, tolerance, annotate, tokenDefinitions);
        var lineCount = outputFile.WriteLinesAsync(reassembledLines);

        await foreach (var line in lineCount) {
            yield return $"Output lines: {line}";
        }

        yield return "Success";
    }
}