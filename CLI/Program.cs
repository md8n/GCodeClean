// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;

using GCodeClean.IO;
using GCodeClean.Processing;

namespace GCodeClean.CLI
{
    class Program
    {
        class Options
        {
            [Option("filename", Required = false, HelpText = "Full path to the input filename.")]
            public string filename { get; set; }

            [Usage(ApplicationAlias = "GCodeClean")]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>() {
                        new Example("Convert file to a trendy format", new Options { filename = "file.bin" })
                    };
                }
            }
        }

        public static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                .WithParsedAsync(RunAsync);
            Console.WriteLine($"Exit code= {Environment.ExitCode}");
        }

        private static async Task HandleParseErrorAsync(IEnumerable<Error> errs)
        {
            if (errs.IsVersion())
            {
                Console.WriteLine("Version Request");
                return;
            }

            if (errs.IsHelp())
            {
                Console.WriteLine("Help Request");
                return;
            }
            Console.WriteLine("Parser Fail");
        }

        static async Task RunAsync(Options options)
        {
            var inputFile = options.filename;
            var outputFile = inputFile;
            var inputExtension = Path.GetExtension(inputFile);
            Console.WriteLine(inputExtension);
            if (String.IsNullOrEmpty(inputExtension))
            {
                outputFile += "-gcc.nc";
            }
            else
            {
                outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension);
            }
            Console.WriteLine("Outputting to:" + outputFile);

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.Tokenize()
                .DedupRepeatedTokens()
                .Augment()
                .DedupLinearToArc(0.005M)
                .Clip()
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M)
                .DedupLinear(0.0005M)
                //.Annotate()
                .DedupSelectTokens(new List<char> { 'F', 'Z' })
                .DedupTokens()
                .JoinTokens();
            var lineCount = outputFile.WriteLinesAsync(outputLines);

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
