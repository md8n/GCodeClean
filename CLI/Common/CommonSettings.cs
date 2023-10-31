// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.ComponentModel;

using Spectre.Console.Cli;

namespace GCodeCleanCLI.Common
{
    public class CommonSettings : CommandSettings {
        [CommandOption("-f|--filename <FILENAME>")]
        [Description("Full path to the input filename. This is the [italic]only[/] required option.")]
        public string Filename { get; set; }
    }
}