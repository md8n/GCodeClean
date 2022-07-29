// Copyright (c) 2020-22 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
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
                args = new[] { "--help" };
            }

            try
            {
                await Parser.Default.ParseArguments<Options>(args)
                    .WithParsedAsync(RunAsync).ConfigureAwait(true);
            }
            catch (DirectoryNotFoundException dnfEx)
            {
                Console.WriteLine(dnfEx.Message);
                Environment.ExitCode = 2;
            }
            catch (FileNotFoundException fnfEx)
            {
                Console.WriteLine(fnfEx.Message);
                Environment.ExitCode = 2;
            }

            Console.WriteLine($"Exit code={Environment.ExitCode}");
        }

        private static async Task RunAsync(Options options)
        {
            var tokenDefsPath = options.tokenDefs;
            if (string.IsNullOrWhiteSpace(tokenDefsPath))
            {
                Console.WriteLine("The path to the token definitions JSON file is missing. Proper clipping and annotating of the GCode cannot be performed.");
                return;
            }
            tokenDefsPath = Program.GetCleanTokenDefsPath(tokenDefsPath);

            var tokenDefinitions = Program.LoadAndVerifyTokenDefs(tokenDefsPath);
            if (tokenDefinitions == null)
            {
                return;
            }

            var outputFile = Program.DetermineOutputFilename(options);
            Console.WriteLine("Outputting to:" + outputFile);

            var tolerance = Program.ConstrainOption(options.tolerance, 0.00005M, 0.5M, "Clipping and general mathematical tolerance:");
            var arcTolerance = Program.ConstrainOption(options.arcTolerance, 0.00005M, 0.5M, "Arc simplification tolerance:");
            var zClamp = Program.ConstrainOption(options.zClamp, 0.02M, 10.0M, "Z-axis clamping value (max traveling height):");
            Console.WriteLine("All tolerance and clamping values may be further adjusted to allow for inches vs. millimeters");

            var inputFile = options.filename;
            var inputLines = inputFile.ReadLinesAsync();

            (var minimisationStrategy, var dedupSelection) = Program.GetMinimisationStrategy(options.minimise, new List<char> { 'F', 'Z' });

            var reassembledLines = inputLines.ProcessLines(dedupSelection, minimisationStrategy, options.lineNumbers, options.eliminateNeedlessTravelling, zClamp, arcTolerance, tolerance, options.annotate, tokenDefinitions);
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);

            await foreach (var line in lineCount)
            {
                Console.WriteLine(line);
            }
        }

        private static string GetCleanTokenDefsPath(string tokenDefsPath)
        {
            if (tokenDefsPath.ToUpperInvariant() == "TOKENDEFINITIONS.JSON")
            {
                var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                               ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";
            }
            return tokenDefsPath;
        }

        private static JsonDocument LoadAndVerifyTokenDefs(string tokenDefsPath)
        {
            JsonDocument tokenDefinitions;

            try
            {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
            }
            catch (FileNotFoundException fileNotFoundEx)
            {
                Console.WriteLine($"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
                return null;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}");
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return tokenDefinitions;
        }

        private static string DetermineOutputFilename(Options options)
        {
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

            return outputFile;
        }

        private static decimal ConstrainOption(decimal option, decimal min, decimal max, string msg)
        {
            if (option < min)
            {
                option = min;
            }
            else if (option > max)
            {
                option = max;
            }
            Console.WriteLine(msg + option);

            return option;
        }

        private static (string, List<char>) GetMinimisationStrategy(string minimise, List<char> dedupSelection)
        {
            var minimisationStrategy = string.IsNullOrWhiteSpace(minimise)
                ? "SOFT"
                : minimise.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(minimise) && minimisationStrategy != "SOFT")
            {
                var hardList = new List<char> { 'A', 'B', 'C', 'D', 'F', 'G', 'H', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'X', 'Y', 'Z' };
                dedupSelection = minimisationStrategy == "HARD" || minimisationStrategy == "MEDIUM"
                    ? hardList
                    : new List<char>(minimisationStrategy).Intersect(hardList).ToList();
            }

            return (minimisationStrategy, dedupSelection);
        }
    }
}
