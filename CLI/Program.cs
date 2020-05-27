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
using GCodeClean.Structure;

namespace GCodeCleanCLI
{
    internal static class Program
    {
        private static Context Preamble()
        {
            var context = new Context(
                new List<(Line line, bool isOutput)>
                    {
                        (new Line("G21"), false), // Length units, mm - alternate G20
                        (new Line("G90"), false), // Distance mode, absolute - alternate G91
                        (new Line("G94"), false), // Feed mode, per minute - alternate G93
                        (new Line("G17"), false), // Set plane, XY - alternates G18, G19
                        (new Line("G40"), false), // Cutter radius compensation, off - alternates are G41, G42
                        (new Line("G49"), false), // Tool length offset, none - alternate G43
                        // (new Line("G61"), false), // Path control mode, exact path - alternates are G61.1, G64
                        // (new Line("G80"), false), // Modal motion (AKA Canned Cycle), Cancel - alternates are G81, G82, G83, G84, G85, G86, G87, G88, G89
                        // (new Line("F"), false), // Feed rate, default will depend on length units
                        // (new Line("S"), false), // Spindle speed
                        // (new Line("T"), false), // Select tool
                        // (new Line("M6"), false), // Change tool
                        // (new Line("M3"), false), // Spindle control, clockwise - alternates are M4, M5
                        // (new Line("M7 M8"), false), // Coolant control, mist and flood - alternates are any one of M7, M8, M9 
                    }
            );

            return context;
        }
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

            var inputLines = inputFile.ReadLinesAsync();
            var outputLines = inputLines.TokeniseToLine()
                .DedupRepeatedTokens()
                .SingleCommandPerLine()
                .InjectPreamble(Preamble())
                .Augment()
                .ConvertArcRadiusToCenter()
                .DedupLinearToArc(0.005M)
                .Clip(tokenDefinitions)
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
