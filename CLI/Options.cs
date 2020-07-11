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

        [Option("minimise", Required = false, HelpText = "Select preferred minimisation strategy, 'soft' - (default) FZ only, 'medium' - All codes excluding IJK (but leave spaces in place), 'hard' - All codes excluding IJK and remove spaces, or list of codes e.g. FGXYZ", Default = "soft")]
        public string minimise { get; set; }

        [Option("tolerance", Required = false, HelpText = "Enter a clipping tolerance for the various deduplication operations")]
        public decimal tolerance { get; set; }

        [Option("arcTolerance", Required = false, HelpText = "Enter a tolerance for the 'point-to-point' length of arcs (G2, G3) below which they will be converted to lines (G1)")]
        public decimal arcTolerance { get; set; }

        [Option("zClamp", Required = false, HelpText = "Restrict z-axis positive values to the supplied value")]
        public decimal zClamp { get; set; }

        [Usage(ApplicationAlias = "GCodeClean")]
        public static IEnumerable<Example> Examples => new List<Example> {
            new Example("Clean GCode file", new Options { filename = "facade.nc" })
        };
    }
}