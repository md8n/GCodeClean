// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.IO;

using GCodeClean.IO;
using GCodeClean.Shared;
using GCodeClean.Split;


namespace GCodeCleanCLI.Split
{
    public static class SplitAction {
        private static string DetermineOutputFoldername(this string inputFile) {
            var outputFolderPath = Path.GetDirectoryName(inputFile);
            var outputFolder = Path.GetFileNameWithoutExtension(inputFile);

            return Path.Join(outputFolderPath, outputFolder);
        }

        public static int Execute(FileInfo filename) {
            var inputFile = filename.ToString();

            var outputFolder = inputFile.DetermineOutputFoldername();
            Console.WriteLine($"Outputting to folder: {outputFolder}");

            var inputLines = inputFile.ReadFileLines();

            var travellingComments = inputLines.GetTravellingComments();
            var preambleLines = inputLines.GetPreamble();
            var postambleLines = inputLines.GetPostamble(travellingComments[^1]);

            inputLines.SplitFile(outputFolder, travellingComments, preambleLines, postambleLines);

            Console.WriteLine($"Split completed");

            return 0;
        }
    }
}