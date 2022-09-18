// Copyright (c) 2020-2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using GCodeClean.IO;
using GCodeClean.Processing;

using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI
{
    internal sealed class GCodeClean : AsyncCommand<GCodeClean.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-f|--filename <FILENAME>")]
            [Description("Full path to the input filename. This is the [italic]only[/] required option.")]
            public string Filename { get; set; }

            [CommandOption("--tokenDefs <TOKENDEFS>")]
            [Description("Full path to the [bold][italic]tokenDefinitions.json[/][/] file.")]
            [DefaultValue("tokenDefinitions.json")]
            public string TokenDefs { get; set; }

            [CommandOption("--annotate")]
            [Description("Annotate the GCode with inline comments.")]
            public bool Annotate { get; set; }

            [CommandOption("--lineNumbers")]
            [Description("Keep line numbers")]
            [DefaultValue(false)]
            public bool LineNumbers { get; set; }

            [CommandOption("--minimise <MINIMISE>")]
            [Description("Select preferred minimisation strategy,\r\n[bold][italic]'soft'[/][/] - (default) [bold]FZ[/] only,\r\n[bold][italic]'medium'[/][/] - All codes excluding [bold]IJK[/] (but leave spaces in place),\r\n[bold][italic]'hard'[/][/] - All codes excluding [bold]IJK[/] and remove spaces,\r\nor list of codes e.g. [bold]FGXYZ[/]")]
            [DefaultValue("soft")]
            public string Minimise { get; set; }

            [CommandOption("--tolerance [TOLERANCE]")]
            [Description("Enter a clipping tolerance for the various deduplication operations")]
            public FlagValue<decimal> Tolerance { get; set; }

            [CommandOption("--arcTolerance [ARCTOLERANCE]")]
            [Description("Enter a tolerance for the 'point-to-point' length of arcs ([bold]G2[/], [bold]G3[/]) below which they will be converted to lines ([bold]G1[/])")]
            public FlagValue<decimal> ArcTolerance { get; set; }

            [CommandOption("--zClamp [ZCLAMP]")]
            [Description("Restrict z-axis positive values to the supplied value")]
            public FlagValue<decimal> ZClamp { get; set; }

            [CommandOption("--eliminateNeedlessTravelling")]
            [Description("Eliminate needless 'travelling', extra movements with positive z-axis values")]
            [DefaultValue(true)]
            public bool EliminateNeedlessTravelling { get; set; }

            //[Usage(ApplicationAlias = "GCodeClean")]
            //public static IEnumerable<Example> Examples => new List<Example> {
            //    new Example("Clean GCode file", new Options { Filename = "facade.nc" })
            //};


            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(TokenDefs))
                {
                    return ValidationResult.Error("[bold yellow]The path to the token definitions JSON file is missing. Proper clipping and annotating of the GCode cannot be performed.[/]");
                }

                var tokenDefsPath = GetCleanTokenDefsPath(TokenDefs);
                var (tokenDefinitions, errorResult) = LoadAndVerifyTokenDefs(tokenDefsPath);
                if (tokenDefinitions == null)
                {
                    return ValidationResult.Error(errorResult);
                }

                return ValidationResult.Success();
            }

            public static string GetCleanTokenDefsPath(string tokenDefsPath)
            {
                if (tokenDefsPath.ToUpperInvariant() == "TOKENDEFINITIONS.JSON")
                {
                    var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                                   ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";
                }
                return tokenDefsPath;
            }

            public static (JsonDocument, string) LoadAndVerifyTokenDefs(string tokenDefsPath)
            {
                JsonDocument tokenDefinitions;

                try
                {
                    var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                    tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
                }
                catch (FileNotFoundException fileNotFoundEx)
                {
                    return (null, $"[bold yellow]No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}[/]");
                }
                catch (JsonException jsonEx)
                {
                    return (null, $"[bold yellow]The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}[/]");
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLine($"[bold yellow]{e}[/]");
                    throw;
                }

                return (tokenDefinitions, "");
            }

            public static string DetermineOutputFilename(Settings options)
            {
                var inputFile = options.Filename;
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

            public static decimal ConstrainOption(FlagValue<decimal> option, decimal min, decimal max, string msg)
            {
                var value = min;
                if (option.IsSet) {
                    if (option.Value < min) {
                        value = min;
                    } else if (option.Value > max) {
                        value = max;
                    }
                }
                AnsiConsole.MarkupLine($"{msg} [bold yellow]{value}[/]");

                return value;
            }

            public static (string, List<char>) GetMinimisationStrategy(string minimise, List<char> dedupSelection)
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

        public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
        {
            var outputFile = Settings.DetermineOutputFilename(settings);
            AnsiConsole.MarkupLine($"Outputting to: [bold green]{outputFile}[/]");

            var tolerance = Settings.ConstrainOption(settings.Tolerance, 0.00005M, 0.5M, "Clipping and general mathematical tolerance:");
            var arcTolerance = Settings.ConstrainOption(settings.ArcTolerance, 0.00005M, 0.5M, "Arc simplification tolerance:");
            var zClamp = Settings.ConstrainOption(settings.ZClamp, 0.02M, 10.0M, "Z-axis clamping value (max traveling height):");
            AnsiConsole.MarkupLine("[blue]All tolerance and clamping values may be further adjusted to allow for inches vs. millimeters[/]");

            var inputFile = settings.Filename;
            var inputLines = inputFile.ReadLinesAsync();

            var (minimisationStrategy, dedupSelection) = Settings.GetMinimisationStrategy(settings.Minimise, new List<char> { 'F', 'Z' });

            var tokenDefsPath = Settings.GetCleanTokenDefsPath(settings.TokenDefs);
            var (tokenDefinitions, _) = Settings.LoadAndVerifyTokenDefs(tokenDefsPath);
            var reassembledLines = inputLines.ProcessLines(dedupSelection, minimisationStrategy, settings.LineNumbers, settings.EliminateNeedlessTravelling, zClamp, arcTolerance, tolerance, settings.Annotate, tokenDefinitions);
            var lineCount = outputFile.WriteLinesAsync(reassembledLines);

            await foreach (var line in lineCount)
            {
                AnsiConsole.MarkupLine($"Output lines: [bold yellow]{line}[/]");
            }

            return 0;
        }
    }
}