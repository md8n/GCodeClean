// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using GCodeClean.IO;
using GCodeClean.Processing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI.Split
{
    public class SplitCommand : AsyncCommand<SplitSettings> {

        public static string DetermineOutputFoldername(SplitSettings options) {
            var inputFile = options.Filename;

            var outputFolder = Path.GetFileNameWithoutExtension(inputFile);

            return outputFolder;
        }

        public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] SplitSettings settings) {
            var outputFolder = DetermineOutputFoldername(settings);
            AnsiConsole.MarkupLine($"Outputting to folder: [bold green]{outputFolder}[/]");

            var inputFile = settings.Filename;
            var inputLines = inputFile.ReadLinesAsync();

            var reassembledLines = inputLines.SplitFile();

            // await foreach (var line in reassembledLines) {
            //     AnsiConsole.MarkupLine($"Output lines: [bold yellow]{line}[/]");
            // }

            return 0;
        }
    }
}