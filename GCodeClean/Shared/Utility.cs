// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using GCodeClean.Processing;
using GCodeClean.Structure;


namespace GCodeClean.Shared
{
    public static partial class Utility {
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

        public static string IdFormat(this int idCount) => $"D{idCount.ToString().Length}";

        public static string NodeFileName(this Node node, string folderName, string idFtm) {
            return $"{folderName}{Path.DirectorySeparatorChar}{node.Tool}_{node.Id.ToString(idFtm)}_{node.Start.ToXYCoord()}_{node.End.ToXYCoord()}_gcc.nc";
        }

        public static Node ParseTravelling(this string travelling) {
            var tDetails = travelling.Replace("(||Travelling||", "").Replace("||)", "").Split("||");
            var tTool = tDetails[0];
            var tId = Convert.ToInt16(tDetails[1]);
            var tSE = tDetails[2].Split(">>", StringSplitOptions.RemoveEmptyEntries);
            var lStart = new Line(tSE[0]);
            var lEnd = new Line(tSE[1]);

            return new Node(tTool, tId, (Coord)lStart, (Coord)lEnd);
        }
    }
}
