// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Diagnostics.CodeAnalysis;
using System.IO;

using Spectre.Console;
using Spectre.Console.Cli;

using GCodeClean.IO;
using GCodeClean.Shared;
using GCodeClean.Split;


namespace GCodeCleanCLI.Split
{
    public class SplitCommand : Command<SplitSettings> {

        public static string DetermineOutputFoldername(SplitSettings options) {
            var inputFile = options.Filename;

            var outputFolderPath = Path.GetDirectoryName(inputFile);
            var outputFolder = Path.GetFileNameWithoutExtension(inputFile);

            return Path.Join(outputFolderPath, outputFolder);
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] SplitSettings settings) {
            var outputFolder = DetermineOutputFoldername(settings);
            AnsiConsole.MarkupLine($"Outputting to folder: [bold green]{outputFolder}[/]");

            var inputFile = settings.Filename;
            var inputLines = inputFile.ReadFileLines();

            var travellingComments = inputLines.GetTravellingComments();
            var preambleLines = inputLines.GetPreamble();
            var postambleLines = inputLines.GetPostamble(travellingComments[^1]);

            inputLines.SplitFile(outputFolder, travellingComments, preambleLines, postambleLines);

            AnsiConsole.MarkupLine($"Split completed");

            return 0;
        }
    }
}