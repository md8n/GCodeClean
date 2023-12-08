// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.ComponentModel;

using Spectre.Console.Cli;

namespace GCodeCleanCLI.Merge
{
    public class MergeSettings : CommandSettings {
        [CommandOption("-f|--foldername <FILENAME>")]
        [Description("Full path to the input folder. This is the [italic]only[/] required option.")]
        public string Foldername { get; set; }
    }
}