using System.Collections.Generic;

using CommandLine;
using CommandLine.Text;

namespace GCodeClean.CLI
{
    class Options
{
        [Option("filename", Required = true, HelpText = "Full path to the input filename.")]
        public string filename { get; set; }

        [Option("annotate", Required = false, HelpText = "Annotate the GCode with inline comments.")]
        public bool annotate { get; set; }

        [Option("minimise", Required = false, HelpText = "Select preferred minimisation strategy, 'soft' - (default) FZ only, 'hard' - All codes, or list of codes e.g. FGXYZIJK")]
        public string minimise { get; set; }

        [Usage(ApplicationAlias = "GCodeClean")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Clean GCode file", new Options { filename = "facade.nc" })
                };
            }
        }
    }
}