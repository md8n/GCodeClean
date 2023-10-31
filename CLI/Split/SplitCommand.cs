// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using GCodeClean.IO;

using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI.Split
{
    public class SplitCommand : AsyncCommand<SplitSettings> {

        public static string DetermineOutputFilename(SplitSettings options) {
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

        public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] SplitSettings settings) {
            var outputFile = DetermineOutputFilename(settings);
            AnsiConsole.MarkupLine($"Outputting to: [bold green]{outputFile}[/]");

            var inputFile = settings.Filename;
            var inputLines = inputFile.ReadLinesAsync();

            var lineCount = outputFile.WriteLinesAsync(inputLines);

            await foreach (var line in lineCount) {
                AnsiConsole.MarkupLine($"Output lines: [bold yellow]{line}[/]");
            }

            return 0;
        }
    }
}