// Copyright (c) 2020-23 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Threading.Tasks;

using GCodeCleanCLI.Clean;
using GCodeCleanCLI.Split;

using Spectre.Console.Cli;

namespace GCodeCleanCLI
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandApp();
            //app.SetDefaultCommand<CleanCommand>();
            app.Configure(config => {
                config.ValidateExamples();

                config.AddBranch<CleanSettings>("clean", clean => {
                    clean.AddCommand<Clean.CleanCommand>("clean")
                        .WithDescription("Clean your GCode file. This is the default command");
                });

                config.AddBranch<SplitSettings>("split", split => {
                    split.AddCommand<Split.SplitCommand>("split")
                        .WithDescription("Split your GCode file into individual cutting actions");
                });
            });

            if (args.Length == 0) {
                args = new[] { "-h" };
            }

            return await app.RunAsync(args);
        }
    }
}
