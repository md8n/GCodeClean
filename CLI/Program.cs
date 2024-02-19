// Copyright (c) 2020-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Actions.Clean;
using Actions.Split;
using Actions.Merge;


namespace GCodeCleanCLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var filenameOption = new Option<FileInfo?>(
            name: "--filename",
            description: "Full path to the input filename"
            ) { IsRequired = true };

        var folderOption = new Option<DirectoryInfo?>(
            name: "--folder",
            description: "Full path to the input folder"
            ) { IsRequired = true };

        var tokenDefsOption = new Option<FileInfo?>(
            name: "--tokenDefs",
            description: "Full path to the tokenDefinitions.json file",
            isDefault: true,
            parseArgument: result => {
                FileInfo tokDef;
                if (!result.Tokens.Any()) {
                    tokDef = new FileInfo("tokenDefinitions.json");
                } else {
                    tokDef = new FileInfo(result.Tokens.Single().Value);
                }
                var tokenDefsPath = tokDef.GetCleanTokenDefsPath();
                var (tokenDefinitions, errorResult) = tokenDefsPath.LoadAndVerifyTokenDefs();
                if (tokenDefinitions == null) {
                    result.ErrorMessage = errorResult;
                }
                return tokenDefsPath;
            });

        var annotateOption = new Option<bool>(
            name: "--annotate",
            description: "Annotate the GCode with inline comments",
            getDefaultValue: () => false);

        var lineNumbersOption = new Option<bool>(
            name: "--lineNumbers",
            description: "Keep line numbers",
            getDefaultValue: () => false);

        var minimiseOption = new Option<string>(
        name: "--minimise",
            description: "Select preferred minimisation strategy,\r\n'soft' - (default) FZ only,\r\n'medium' - All codes excluding IJK(but leave spaces in place),\r\n'hard' - All codes excluding IJK and remove spaces,\r\nor list of codes e.g.FGXYZ",
            getDefaultValue: () => "soft");

        var toleranceOption = new Option<decimal>(
            name: "--tolerance",
            description: "Enter a clipping tolerance for the various deduplication operations. Default value ultimately depends on the units");

        var arcToleranceOption = new Option<decimal>(
            name: "--arcTolerance",
            description: "Enter a tolerance for the 'point-to-point' length of arcs (G2, G3) below which they will be converted to lines (G1)");

        var zClampOption = new Option<decimal>(
            name: "--zClamp",
            description: "Restrict z-axis positive values to the supplied value");

        var rootCommand = new RootCommand("GCodeClean");

        var cleanCommand = new Command("clean", "Clean your GCode file.")
        {
            filenameOption,
            tokenDefsOption,
            annotateOption,
            lineNumbersOption,
            minimiseOption,
            toleranceOption,
            arcToleranceOption,
            zClampOption
        };
        rootCommand.AddCommand(cleanCommand);
        cleanCommand.SetHandler(async (filename, tokenDefs, annotate, lineNumbers, minimise, tolerance, arcTolerance, zClamp) => {
            await RunCleanAsync(filename!, tokenDefs, annotate, lineNumbers, minimise, tolerance, arcTolerance, zClamp);
        },
        filenameOption, tokenDefsOption, annotateOption, lineNumbersOption, minimiseOption, toleranceOption, arcToleranceOption, zClampOption);

        var splitCommand = new Command("split", "Split your GCode file into individual cutting actions.") { filenameOption };
        rootCommand.AddCommand(splitCommand);
        splitCommand.SetHandler((filename) => { RunSplit(filename!); }, filenameOption);

        var mergeCommand = new Command("merge", "Merge a folder of files, produced by split, back into a single GCode file.") { folderOption };
        rootCommand.AddCommand(mergeCommand);
        mergeCommand.SetHandler((folder) => { RunMerge(folder!); }, folderOption);

        return await rootCommand.InvokeAsync(args);
    }

    internal static void DoLogging(string logMessage) {
        Console.WriteLine(logMessage);
    }

    //async method
    internal static async Task<int> RunCleanAsync(
        FileInfo filename,
        FileInfo tokenDefs,
        bool annotate,
        bool lineNumbers,
        string minimise,
        decimal tolerance,
        decimal arcTolerance,
        decimal zClamp
    ) {
        (tolerance, arcTolerance, zClamp) = CleanOptions.Constrain(tolerance, arcTolerance, zClamp);
        DoLogging($"Clipping and general mathematical tolerance: {tolerance}");
        DoLogging($"Arc simplification tolerance: {tolerance}");
        DoLogging($"Z-axis clamping value (max traveling height): {tolerance}");
        DoLogging("All tolerance and clamping values may be further adjusted to allow for inches vs. millimeters");
        var (tokenDefinitions, _) = tokenDefs.LoadAndVerifyTokenDefs();

        string lastMessage = "";
        await foreach (string logMessage in CleanAction.ExecuteAsync(filename, annotate, lineNumbers, minimise, tolerance, arcTolerance, zClamp, tokenDefinitions)) {
            DoLogging(logMessage);
            lastMessage = logMessage;
        }
        return lastMessage == "Success" ? 0 : 1;
    }

    internal static async Task<int> RunSplit(FileInfo filename) {
        string lastMessage = "";
        await foreach(string logMessage in SplitAction.ExecuteAsync(filename)) {
            DoLogging(logMessage);
            lastMessage = logMessage;
        }
        return lastMessage == "Success" ? 0 : 1;
    }

    internal static async Task<int> RunMerge(DirectoryInfo folder) {
        string lastMessage = "";
        await foreach (string logMessage in MergeAction.ExecuteAsync(folder)) {
            DoLogging(logMessage);
            lastMessage = logMessage;
        }
        return lastMessage == "Success" ? 0 : 1;
    }
}
