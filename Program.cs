// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
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
            var outputFile = inputFile;
            var inputExtension = Path.GetExtension(inputFile);
            Console.WriteLine(inputExtension);
            if (String.IsNullOrEmpty(inputExtension)) {
                outputFile += "-gcc.nc";
            } else {
                outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension);
            }
            Console.WriteLine("Outputting to:" + outputFile);

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.Tokenize()
                .Clip()
                .Augment()
                .Dedup()
                .DedupLinear(0.0005)
                // .DedupLinear(0.0005)
                // .DedupLinear(0.0005)
                // .DedupLinear(0.0005)
                //.Annotate()
                .DedupSelectTokens(new List<char> {'F', 'Z'})
                //.DedupTokens()
                .JoinTokens();
            var lineCount = outputFile.WriteLinesAsync(outputLines);

            await foreach(var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }
    }
}
