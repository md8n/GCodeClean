// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;

using CommandLine;
using CommandLine.Text;

namespace GCodeCleanCLI
{
    internal class Options
    {
        [Option("filename", Required = true, HelpText = "Full path to the input filename.")]
        public string filename { get; set; }

        [Option("tokenDefs", Required = false, HelpText = "Full path to the tokenDefinitions.json file.", Default = "tokenDefinitions.json")]
        public string tokenDefs { get; set; }

        [Option("annotate", Required = false, HelpText = "Annotate the GCode with inline comments.")]
        public bool annotate { get; set; }

        [Option("minimise", Required = false, HelpText = "Select preferred minimisation strategy, 'soft' - (default) FZ only, 'medium' - All codes (but leave spaces in place), 'hard' - All codes and remove spaces, or list of codes e.g. FGXYZIJK", Default = "soft")]
        public string minimise { get; set; }

        [Usage(ApplicationAlias = "GCodeClean")]
        public static IEnumerable<Example> Examples => new List<Example> {
            new Example("Clean GCode file", new Options { filename = "facade.nc" })
        };
    }
}