// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.IO;

using Spectre.Console;

using GCodeClean.Processing;
using GCodeClean.Structure;
using GCodeClean.Shared;


namespace GCodeClean.Split
{
    public static partial class Split {
        public static void SplitFile(this IEnumerable<string> inputLines, string outputFolder, List<string> travellingComments, List<string> preambleLines, List<string> postambleLines) {
            if (Directory.Exists(outputFolder)) {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            var (_, tLId, _, _) = travellingComments[^1].ParseTravelling();
            var idFtm = tLId.IdFormat();

            string firstLine = "";

            var iL = inputLines.GetEnumerator();

            while (iL.MoveNext()) {
                var line = iL.Current;
                if (line == Default.PreambleCompleted) {
                    break;
                }
            }

            foreach (var travelling in travellingComments) {
                var filename = travelling.ParseTravelling().NodeFileName(outputFolder, idFtm);
                AnsiConsole.MarkupLine($"Filename: [bold yellow]{filename}[/]");

                File.WriteAllLines(filename, preambleLines);

                if (firstLine != "") {
                    File.AppendAllLines(filename, [firstLine]);
                }

                while (iL.MoveNext()) {
                    var line = iL.Current;
                    File.AppendAllLines(filename, [line]);
                    if (line.EndsWith(travelling)) {
                        firstLine = (new Line(line)).ToSimpleString();
                        break;
                    }
                }

                File.AppendAllLines(filename, postambleLines);
            }
        }
    }
}
