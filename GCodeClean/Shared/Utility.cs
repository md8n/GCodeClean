// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

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
        [GeneratedRegex("\\(\\|{2}Travelling(\\|{2}\\d+){3}\\|{2}\\-?\\d+.*\\|{2}.*\\|{2}>>G\\d+.*>>G\\d+.*>>\\|{2}\\)$")]
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

        public static string NodeFileName(this Node node, string folderName, int[] idCounts) {
            var seqFtm = idCounts[0].IdFormat();
            var subSeqFtm = idCounts[1].IdFormat();
            var idFtm = idCounts[2].IdFormat();
            return $"{folderName}{Path.DirectorySeparatorChar}{node.Seq.ToString(seqFtm)}_{node.SubSeq.ToString(subSeqFtm)}_{node.Id.ToString(idFtm)}_{node.Tool}_{node.Start.ToXYCoord()}_{node.End.ToXYCoord()}_gcc.nc";
        }

        public static Node ToNode(this string travelling) {
            var tDetails = travelling.Replace("(||Travelling||", "").Replace("||)", "").Split("||");
            var tSeq = short.Parse(tDetails[0]);
            var tSubSeq = short.Parse(tDetails[1]);
            var tId = short.Parse(tDetails[2]);
            var tMaxZ = decimal.Parse(tDetails[3]);
            var tTool = tDetails[4];
            var tSE = tDetails[5].Split(">>", StringSplitOptions.RemoveEmptyEntries);
            var lStart = new Line(tSE[0]);
            var lEnd = new Line(tSE[1]);

            return new Node(tSeq, tSubSeq, tId, tMaxZ, tTool, (Coord)lStart, (Coord)lEnd);
        }

        public static string ToTravelling(this Node node) {
            var entryLine = $"G0 {node.Start.ToString()}";
            var exitLine = $"G0 {node.End.ToString()}";
            return $"(||Travelling||{node.Seq}||{node.SubSeq}||{node.Id}||{node.MaxZ:0.###}||{node.Tool}||>>{entryLine}>>{exitLine}>>||)";
        }

        /// <summary>
        /// Copy the node, but set its SubSeq value to the supplied value
        /// </summary>
        /// <param name="node"></param>
        /// <param name="subSeq"></param>
        /// <returns></returns>
        public static Node CopySetSub(this Node node, short subSeq) => new(node.Seq, subSeq, node.Id, node.MaxZ, node.Tool, node.Start, node.End);
    }
}
