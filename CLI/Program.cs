// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using CommandLine;
using Newtonsoft.Json.Linq;

using GCodeClean.IO;
using GCodeClean.Processing;

namespace GCodeCleanCLI
{
    internal static class Program
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
            var tokenDefsPath = options.tokenDefs;
            if (string.IsNullOrWhiteSpace(tokenDefsPath))
            {
                Console.WriteLine("The path to the token definitions JSON file is missing. Proper clipping and annotating of the GCode cannot be performed.");
                return;
            }
            if (tokenDefsPath.ToUpperInvariant() == "TOKENDEFINITIONS.JSON")
            {
                var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                               ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";
            }

            JObject tokenDefinitions;
            try
            {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JObject.Parse(tokenDefsSource);
            }
            catch (FileNotFoundException fileNotFoundEx)
            {
                Console.WriteLine($"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
                return;
            }
            catch (Newtonsoft.Json.JsonReaderException jsonReaderEx)
            {
                Console.WriteLine($"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonReaderEx.Message}");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var inputFile = options.filename;
            var outputFile = inputFile;
            var inputExtension = Path.GetExtension(inputFile);
            if (string.IsNullOrEmpty(inputExtension))
            {
                outputFile += "-gcc.nc";
            }
            else
            {
                outputFile = outputFile.Replace(inputExtension, "-gcc" + inputExtension, StringComparison.InvariantCultureIgnoreCase);
            }
            Console.WriteLine("Outputting to:" + outputFile);

            if (options.tolerance < 0.0005M) {
                options.tolerance = 0.0005M;
            } else if (options.tolerance > 0.5M) {
                options.tolerance = 0.5M;
            }
            Console.WriteLine("Clipping and general mathematical tolerance:" + options.tolerance);

            if (options.arcTolerance < 0.0005M) {
                options.arcTolerance = 0.0005M;
            } else if (options.arcTolerance > 0.5M) {
                options.arcTolerance = 0.5M;
            }
            Console.WriteLine("Arc simplification tolerance:" + options.arcTolerance);

            var linearToArcTolerance = options.tolerance * 10;

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.TokeniseToLine()
                .DedupRepeatedTokens()
                .SingleCommandPerLine()
                .InjectPreamble(Default.Preamble())
                .Augment()
                .ConvertArcRadiusToCenter()
                .SimplifyShortArcs(options.arcTolerance)
                .DedupLinearToArc(linearToArcTolerance)
                .Clip(options.tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance);

            var minimisationStrategy = string.IsNullOrWhiteSpace(options.minimise)
                ? "SOFT"
                : options.minimise.ToUpperInvariant();
            var dedupSelection = new List<char> { 'F', 'Z' };
            if (!string.IsNullOrWhiteSpace(options.minimise) && minimisationStrategy != "SOFT")
            {
                var hardList = new List<char> { 'A', 'B', 'C', 'D', 'F', 'G', 'H', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z' };
                dedupSelection = minimisationStrategy == "HARD" || minimisationStrategy == "MEDIUM"
                    ? hardList
                    : new List<char>(minimisationStrategy).Intersect(hardList).ToList();
            }

            var minimisedLines = outputLines.DedupSelectTokens(dedupSelection);

            var annotatedLines = options.annotate ? minimisedLines.Annotate(tokenDefinitions) : minimisedLines;
            var reassembledLines = annotatedLines.JoinLines(minimisationStrategy);
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);

            await foreach (var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }
    }
}
