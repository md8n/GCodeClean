// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

using GCodeCleanCLI.Common;

using Spectre.Console;
using Spectre.Console.Cli;

namespace GCodeCleanCLI.Clean
{
    public sealed class CleanSettings : CommonSettings {
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

        //[CommandOption("--eliminateNeedlessTravelling")]
        //[Description("Eliminate needless 'travelling', extra movements with positive z-axis values")]
        //[DefaultValue(false)]
        //public bool EliminateNeedlessTravelling { get; set; }

        public override ValidationResult Validate() {
            if (string.IsNullOrWhiteSpace(TokenDefs)) {
                return ValidationResult.Error("[bold yellow]The path to the token definitions JSON file is missing. Proper clipping and annotating of the GCode cannot be performed.[/]");
            }

            var tokenDefsPath = GetCleanTokenDefsPath(TokenDefs);
            var (tokenDefinitions, errorResult) = LoadAndVerifyTokenDefs(tokenDefsPath);
            if (tokenDefinitions == null) {
                return ValidationResult.Error(errorResult);
            }

            return ValidationResult.Success();
        }

        public static string GetCleanTokenDefsPath(string tokenDefsPath) {
            if (tokenDefsPath.Equals("TOKENDEFINITIONS.JSON", StringComparison.InvariantCultureIgnoreCase)) {
                var entryDir = Path.GetDirectoryName(AppContext.BaseDirectory);

                tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";
            }
            return tokenDefsPath;
        }

        public static (JsonDocument, string) LoadAndVerifyTokenDefs(string tokenDefsPath) {
            JsonDocument tokenDefinitions;

            try {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
            } catch (FileNotFoundException fileNotFoundEx) {
                return (null, $"[bold yellow]No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}[/]");
            } catch (JsonException jsonEx) {
                return (null, $"[bold yellow]The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}[/]");
            } catch (Exception e) {
                AnsiConsole.MarkupLine($"[bold yellow]{e}[/]");
                throw;
            }

            return (tokenDefinitions, "");
        }
    }
}