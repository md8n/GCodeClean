// Copyright (c) 2020-22 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace GCodeCleanCLI
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var app = new CommandApp();
            app.SetDefaultCommand<GCodeClean>();
            app.Configure(config => {
                config.ValidateExamples();

                config.AddCommand<GCodeClean>("clean")
                    .WithDescription("Clean your GCode file. This is the default command")
                    .WithExample(new[] { "clean", "--filename", "facade.nc" });
            });

            if (args.Length == 0)
            {
                args = new[] { "-h" };
            }

            return await app.RunAsync(args);
        }
    }
}
