// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using GCodeClean.IO;
using GCodeClean.Processing;

using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI.Clean
{
    public class CleanCommand : AsyncCommand<CleanSettings> {
        public static string DetermineOutputFilename(CleanSettings options) {
            var inputFile = options.Filename;
            var outputFile = inputFile;

            var inputExtension = Path.GetExtension(inputFile);
            if (string.IsNullOrEmpty(inputExtension)) {
                outputFile += "-gcc.nc";
            } else {
                outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension, StringComparison.InvariantCultureIgnoreCase);
            }

            return outputFile;
        }

        public static decimal ConstrainOption(FlagValue<decimal> option, decimal min, decimal max, string msg) {
            var value = min;
            if (option.IsSet) {
                if (option.Value < min) {
                    value = min;
                } else if (option.Value > max) {
                    value = max;
                }
            }
            AnsiConsole.MarkupLine($"{msg} [bold yellow]{value}[/]");

            return value;
        }

        public static (string, List<char>) GetMinimisationStrategy(string minimise, List<char> dedupSelection) {
            var minimisationStrategy = string.IsNullOrWhiteSpace(minimise)
                ? "SOFT"
                : minimise.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(minimise) && minimisationStrategy != "SOFT") {
                List<char> hardList = ['A', 'B', 'C', 'D', 'F', 'G', 'H', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z'];
                dedupSelection = minimisationStrategy == "HARD" || minimisationStrategy == "MEDIUM"
                    ? hardList
                    : new List<char>(minimisationStrategy).Intersect(hardList).ToList();
            }

            return (minimisationStrategy, dedupSelection);
        }

        public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] CleanSettings settings) {
            var inputFile = settings.Filename;
            if (!File.Exists(inputFile)) {
                return 1;
            }
            var outputFile = DetermineOutputFilename(settings);
            AnsiConsole.MarkupLine($"Outputting to: [bold green]{outputFile}[/]");

            var tolerance = ConstrainOption(settings.Tolerance, 0.00005M, 0.5M, "Clipping and general mathematical tolerance:");
            var arcTolerance = ConstrainOption(settings.ArcTolerance, 0.00005M, 0.5M, "Arc simplification tolerance:");
            var zClamp = ConstrainOption(settings.ZClamp, 0.02M, 10.0M, "Z-axis clamping value (max traveling height):");
            AnsiConsole.MarkupLine("[blue]All tolerance and clamping values may be further adjusted to allow for inches vs. millimeters[/]");

            var inputLines = inputFile.ReadLinesAsync();

            var (minimisationStrategy, dedupSelection) = GetMinimisationStrategy(settings.Minimise, ['F', 'Z']);
            //AnsiConsole.MarkupLine($"Minimisation strategy: [bold yellow]{minimisationStrategy}[/]");
            //AnsiConsole.MarkupLine($"Deduoplication selection: [bold yellow]{dedupSelection}[/]");

            var tokenDefsPath = CleanSettings.GetCleanTokenDefsPath(settings.TokenDefs);
            AnsiConsole.MarkupLine("blag");
            var (tokenDefinitions, _) = CleanSettings.LoadAndVerifyTokenDefs(tokenDefsPath);
            AnsiConsole.MarkupLine("bleg");
            var reassembledLines = inputLines.CleanLines(dedupSelection, minimisationStrategy, settings.LineNumbers, settings.EliminateNeedlessTravelling, zClamp, arcTolerance, tolerance, settings.Annotate, tokenDefinitions);
            AnsiConsole.MarkupLine("blig");
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);
            AnsiConsole.MarkupLine($"{lineCount}");

            await foreach (var line in lineCount) {
                AnsiConsole.MarkupLine($"Output lines: [bold yellow]{line}[/]");
            }

            return 0;
        }
    }
}