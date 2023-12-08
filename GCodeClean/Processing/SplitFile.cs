// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using GCodeClean.Structure;

using Spectre.Console;

namespace GCodeClean.Processing
{
    public static partial class Split {
        /// <summary>
        /// Finds GCodeClean's special 'Travelling' comments
        /// </summary>
        [GeneratedRegex("\\(\\|{2}Travelling\\|{2}.*\\|{2}\\d+\\|{2}>>G\\d+.*>>G\\d+.*>>\\|{2}\\)$")]
        private static partial Regex RegexTravellingPattern();

        /// <summary>
        /// Scan through the file for 'travelling' comments and build a list of them
        /// </summary>
        /// <param name="inputLines"></param>
        /// <returns></returns>
        public static List<string> GetTravellingComments(this IEnumerable<string> inputLines) {
            List<string> travellingComments = [];

            foreach (var line in inputLines) {
                var match = RegexTravellingPattern().Match(line);
                if (match.Success) {
                    travellingComments.Add(match.Value);
                }
            }

            return travellingComments;
        }

        /// <summary>
        /// Get every line up to and including the preamble completed comment
        /// </summary>
        /// <param name="inputLines"></param>
        /// <returns></returns>
        public static List<string> GetPreamble(this IEnumerable<string> inputLines) {
            List<string> preambleLines = [];
            var preambleCompletionFound = false;
            var ixExcess = 0;

            foreach (var line in inputLines) {
                preambleLines.Add(line);
#pragma warning disable S2589
                if (ixExcess++ > 100) {
                    break;
                }
#pragma warning restore S2589
                if (line == Default.PreambleCompleted) {
                    preambleCompletionFound = true;
                    break;
                }
            }

            if (!preambleCompletionFound) {
                return [];
            }

            return preambleLines;
        }

        /// <summary>
        /// Get every line after the final travelling comment
        /// </summary>
        /// <param name="inputLines"></param>
        /// <returns></returns>
        public static List<string> GetPostamble(this IEnumerable<string> inputLines, string finalTravellingComment) {
            List<string> postambleLines = [];
            var finalTravellingFound = false;

            foreach (var line in inputLines) {
                if (line.EndsWith(finalTravellingComment)) {
                    finalTravellingFound = true;
                    continue;
                }
                if (!finalTravellingFound) {
                    continue;
                }
                postambleLines.Add(line);
            }

            return postambleLines;
        }

        public static void SplitFile(this IEnumerable<string> inputLines, string outputFolder, List<string> travellingComments, List<string> preambleLines, List<string> postambleLines) {
            if (Directory.Exists(outputFolder)) {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            var (_, tLId, _, _) = travellingComments[^1].ParseTravelling();
            var idFtm = $"D{tLId.ToString().Length}";

            string firstLine = "";

            var iL = inputLines.GetEnumerator();

            while (iL.MoveNext()) {
                var line = iL.Current;
                if (line == Default.PreambleCompleted) {
                    break;
                }
            }

            foreach (var travelling in travellingComments) {
                var (tool, id, start, end) = travelling.ParseTravelling();
                var filename = $"{outputFolder}{Path.DirectorySeparatorChar}{tool}_{id.ToString(idFtm)}_{start.ToXYCoord()}_{end.ToXYCoord()}_gcc.nc";
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

        private static (string tool, int id, Line start, Line end) ParseTravelling(this string travelling) {
            var tDetails = travelling.Replace("(||Travelling||", "").Replace("||)", "").Split("||");
            var tTool = tDetails[0];
            var tId = Convert.ToInt32(tDetails[1]);
            var tSE = tDetails[2].Split(">>", StringSplitOptions.RemoveEmptyEntries);
            var lStart = new Line(tSE[0]);
            var lEnd = new Line(tSE[1]);

            return (tTool, tId, lStart, lEnd);
        }
    }
}
