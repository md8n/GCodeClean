// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.IO;

using GCodeClean.Merge;


namespace GCodeCleanCLI.Merge
{
    public static class MergeAction {
        public static int Execute(DirectoryInfo folder) {
            var inputFolder = folder.ToString();
            Console.WriteLine($"Inputting from folder: {inputFolder}");

            inputFolder.MergeFile();

            Console.WriteLine("Merge completed");

            return 0;
        }
    }
}
