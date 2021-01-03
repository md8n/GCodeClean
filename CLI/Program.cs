// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using CommandLine;

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

            JsonDocument tokenDefinitions;
            try
            {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
            }
            catch (FileNotFoundException fileNotFoundEx)
            {
                Console.WriteLine($"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
                return;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}");
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

            if (options.zClamp > 10.0M) {
                options.zClamp = 10.0M;
            } else if (options.zClamp > 0 && options.zClamp < 0.02M) {
                options.zClamp = 0.02M;
            }
            Console.WriteLine("Z-axis clamping value (max traveling height):" + options.zClamp);

            Console.WriteLine("All tolerance and clamping values may be further adjusted to allow for inches vs. millimeters");

            var linearToArcTolerance = options.tolerance * 10;
            var context = Default.Preamble();

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.TokeniseToLine()
                //.EliminateLineNumbers()
                .DedupRepeatedTokens()
                .SingleCommandPerLine()
                .InjectPreamble(context, options.zClamp)
                .Augment()
                .ZClamp(context, options.zClamp)
                .ConvertArcRadiusToCenter(context)
                .DedupLine()
                .SimplifyShortArcs(context, options.arcTolerance)
                .DedupLinearToArc(context, linearToArcTolerance)
                .Clip(context, options.tolerance)
                .DedupRepeatedTokens()
                .DedupLine()
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance)
                .DedupLinear(options.tolerance)
                ;

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

            var annotatedLines = options.annotate ? minimisedLines.Annotate(tokenDefinitions.RootElement) : minimisedLines;
            var reassembledLines = annotatedLines.JoinLines(minimisationStrategy);
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);

            await foreach (var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }
    }
}
