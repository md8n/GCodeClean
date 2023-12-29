// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;

using GCodeClean.Processing;
using GCodeClean.Shared;


namespace GCodeClean.Split
{
    public static partial class Split {
        public static void SplitFile(this IEnumerable<string> inputLines, string outputFolder, List<string> travellingComments, List<string> preambleLines, List<string> postambleLines) {
            if (Directory.Exists(outputFolder)) {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            var idFtm = travellingComments.Count.IdFormat();

            var iL = inputLines.GetEnumerator();

            while (iL.MoveNext()) {
                var line = iL.Current;
                if (line == Default.PreambleCompleted) {
                    break;
                }
            }

            foreach (var travelling in travellingComments) {
                var filename = travelling.ParseTravelling().NodeFileName(outputFolder, idFtm);
                Console.WriteLine($"Filename: {filename}");

                File.WriteAllLines(filename, preambleLines);

                while (iL.MoveNext()) {
                    var line = iL.Current;
                    File.AppendAllLines(filename, [line]);
                    if (line.EndsWith(travelling)) {
                        break;
                    }
                }

                File.AppendAllLines(filename, postambleLines);
            }
        }
    }
}
