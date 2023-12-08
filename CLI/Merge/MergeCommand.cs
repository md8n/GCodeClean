// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Diagnostics.CodeAnalysis;

using GCodeClean.Processing;

using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI.Merge
{
    public class MergeCommand : Command<MergeSettings> {
        public override int Execute([NotNull] CommandContext context, [NotNull] MergeSettings settings) {
            var inputFolder = settings.Foldername;
            AnsiConsole.MarkupLine($"Inputting from folder: [bold green]{inputFolder}[/]");

            inputFolder.MergeFile();

            AnsiConsole.MarkupLine($"Merge completed");

            return 0;
        }
    }
}