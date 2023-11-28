// Copyright (c) 2020-23 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Threading.Tasks;

using GCodeCleanCLI.Clean;
using GCodeCleanCLI.Split;

using Spectre.Console;
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
            });

            foreach (var arg in args) {
                AnsiConsole.MarkupLine($"Arg: [bold yellow]{arg}[/]");
            }
            if (args.Length == 0) {
                args = new[] { "-h" };
            }

            return await app.RunAsync(args);
        }
    }
}
