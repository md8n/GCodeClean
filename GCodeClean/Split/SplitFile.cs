// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Shared;
using GCodeClean.Structure;


namespace GCodeClean.Split
{
    public static partial class Split {
        public static void SplitFile(this IEnumerable<string> inputLines, string outputFolder, List<string> travellingComments, List<string> preambleLines, List<string> postambleLines) {
            if (Directory.Exists(outputFolder)) {
                Directory.Delete(outputFolder, true);
            }
            Directory.CreateDirectory(outputFolder);

            var nodes = travellingComments.Select(tc => tc.ToNode()).ToList();
            int[] idCounts = [nodes.Select(n => n.Seq).Distinct().Count(), nodes.Select(n => n.SubSeq).Distinct().Count(), nodes.Count];

            var iL = inputLines.GetEnumerator();
            while (iL.MoveNext()) {
                var line = iL.Current;
                if (line == Default.PreambleCompleted) {
                    break;
                }
            }

            var zClampConstrained = 0.05M;
            var context = preambleLines.BuildPreamble();
            var lengthUnits = context.GetLengthUnits();
            Line prevLine = null;

            foreach (var node in nodes) {
                var filename = node.NodeFileName(outputFolder, idCounts);
                Console.WriteLine($"Filename: {filename}");

                File.WriteAllLines(filename, preambleLines);

                var firstLine = true;
                zClampConstrained = Processing.Utility.ConstrictZClamp(lengthUnits, zClampConstrained);

                while (iL.MoveNext()) {
                    var line = iL.Current;
                    if (firstLine) {
                        var checkLine = new Line(line);
                        var prevLineTravelling = prevLine != null && prevLine.HasToken("G0");
                        if (checkLine.HasMovementCommand()) {
                            if (prevLineTravelling) {
                                (prevLine, zClampConstrained) = prevLine.EnforceZClamp(zClampConstrained, lengthUnits);
                            } else {
                                prevLine = new Line($"G0 Z{zClampConstrained}");
                            }
                            (checkLine, zClampConstrained) = checkLine.EnforceZClamp(zClampConstrained, lengthUnits);
                            if (checkLine.HasToken("G0")) {
                                // All good, just move along (pun intended)
                                line = checkLine.ToString();
                            } else {
                                if (checkLine.HasToken("G1")) {
                                    // Create a G0 from this G1 and inject it first
                                    checkLine.ReplaceToken(new Token("G1"), new Token("G0"));
                                    prevLine = checkLine;
                                } else {
                                    // else A G2, G3 or G38.2 - let's hope prevLine is OK as-is
                                    if (!prevLineTravelling) {
                                        // It ain't, therefore we cannot proceed
                                        throw new ConstraintException($"The first 'movement' line in the individual split file '{filename}' is '{checkLine}'.\r\nHowever, the last movement line from the previous indidivual split file was not a 'G0'.\r\nTherefore a valid GCode file cannot be created.");
                                    }
                                }
                                File.AppendAllLines(filename, [prevLine.ToString()]);
                                prevLine = null;
                            }

                            firstLine = false;
                        }
                    }
                    File.AppendAllLines(filename, [line]);
                    // Clearing the subSeq value will allow us to rebuild the travelling comment as it appears in the GCode
                    var unSubSeqNode = node.CopySetSub(0);
                    if (line.EndsWith(unSubSeqNode.ToTravelling())) {
                        prevLine = new Line(line);
                        break;
                    }
                }

                File.AppendAllLines(filename, postambleLines);
            }
        }

        /// <summary>
        /// Enforce the appearance of a +ve constrained Z value on travelling 'G0' lines
        /// Otherwise, leave everything alone.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="zClampConstrained"></param>
        /// <param name="lengthUnits"></param>
        /// <returns></returns>
        private static (Line line, decimal zClampConstrained) EnforceZClamp(this Line line, decimal zClampConstrained, string lengthUnits) {
            if (line == null || !line.HasToken("G0")) {
                return (line, zClampConstrained);
            }
            if (line.HasToken('Z')) {
                zClampConstrained = Processing.Utility.ConstrictZClamp(lengthUnits, (decimal)line.Tokens.Find(t => t.Code == 'Z').Number);
            } else {
                line.AppendToken(new Token($"Z{zClampConstrained}"));
            }
            return (line, zClampConstrained);
        }
    }
}
