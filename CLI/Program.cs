﻿// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Threading.Tasks;

using GCodeCleanCLI.Clean;
using GCodeCleanCLI.Merge;
using GCodeCleanCLI.Split;

using Spectre.Console.Cli;

namespace GCodeCleanCLI
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandApp();
            app.SetDefaultCommand<CleanCommand>();
            app.Configure(config => {
                config.ValidateExamples();

                config.AddCommand<CleanCommand>("clean")
                    .WithDescription("Clean your GCode file. This is the default command");
                config.AddCommand<SplitCommand>("split")
                    .WithDescription("Split your GCode file into individual cutting actions");
                config.AddCommand<MergeCommand>("merge")
                    .WithDescription("Merge a folder of files, produced by split, back into a single GCode file");
            });

            if (args.Length == 0) {
                args = ["-h"];
            }

            return await app.RunAsync(args);
        }
    }
}
