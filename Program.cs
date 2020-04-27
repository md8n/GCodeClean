// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Threading.Tasks;

namespace GCodeClean
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("GCode Clean");
                Console.WriteLine("-----------");
                Console.WriteLine("Usage:");
                Console.WriteLine("gccodeclean <filename>");
                return;
            }

            var inputFile = args[0];
            var outputFile = inputFile.EndsWith(".nc")
                ? inputFile.Replace(".nc", "-gcc.nc")
                : inputFile + "-gcc.nc";

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.Tokenize()
                .Clip()
                .Augment()
                .Dedup()
                .DedupLinear()
                .DedupLinear()
                .DedupLinear()
                .DedupLinear()
                .Annotate()
                .JoinTokens();
            var lineCount = outputFile.WriteLinesAsync(outputLines);

            await foreach(var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }
    }
}
