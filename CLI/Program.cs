// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommandLine;

using GCodeClean.IO;
using GCodeClean.Processing;

namespace CLI
{
    static class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new [] { "--help" };
            }
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(RunAsync).ConfigureAwait(true);
            Console.WriteLine($"Exit code= {Environment.ExitCode}");
        }

        private static async Task RunAsync(Options options)
        {
            var inputFile = options.filename;
            var outputFile = inputFile;
            var inputExtension = Path.GetExtension(inputFile);
            Console.WriteLine(inputExtension);
            if (string.IsNullOrEmpty(inputExtension))
            {
                outputFile += "-gcc.nc";
            }
            else
            {
                outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension, StringComparison.InvariantCultureIgnoreCase);
            }
            Console.WriteLine("Outputting to:" + outputFile);

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.TokenizeToLine()
                .DedupRepeatedTokens()
                .SingleCommandPerLine()
                .Augment()
                .ConvertArcRadiusToCenter()
                .DedupLinearToArc(0.005M)
                .Clip()
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M);

            var minimisationStrategy = string.IsNullOrWhiteSpace(options.minimise)
                ? "SOFT"
                : options.minimise.ToUpperInvariant();
            var dedupSelection = new List<char> { 'F', 'Z' };
            if (!string.IsNullOrWhiteSpace(options.minimise) && minimisationStrategy != "SOFT")
            {
                var hardList = new List<char> { 'A', 'B', 'C', 'D', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z' };
                dedupSelection = minimisationStrategy == "HARD"
                    ? hardList
                    : new List<char>(minimisationStrategy).Intersect(hardList).ToList();
            }

            var minimisedLines = outputLines.DedupSelectTokens(dedupSelection);

            var annotatedLines = options.annotate ? minimisedLines.Annotate() : minimisedLines;
            var reassembledLines = annotatedLines.JoinTokens(minimisationStrategy);
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);

            await foreach (var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }

        static void RunOptions(Options opts)
        {
            //handle options
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }
    }
}
