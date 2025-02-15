// Copyright (c) 2023-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GCodeClean.Shared;
using GCodeClean.Structure;
using GCodeClean.IO;
using GCodeClean.Processing;


namespace GCodeClean.Merge;

public static class NodeFileIO
{
    private static readonly char[] separator = ['_'];

    public static string ToSimpleString(this Edge edge) => $"{edge.PrevId}<->{edge.NextId}";

    public static bool FolderExists(this string inputFolder) {
        return Directory.Exists(inputFolder);
    }

    /// <summary>
    /// Scan through the folder's file names and build a list of nodes from them
    /// </summary>
    /// <param name="inputFolder"></param>
    /// <returns></returns>
    public static List<Node> GetNodes(this string inputFolder) {
        var fileEntries = Directory.GetFiles(inputFolder);
        Array.Sort(fileEntries);
        List<Node> nodes = [];
        foreach (var filePath in fileEntries) {
            var fileNameParts = Path.GetFileNameWithoutExtension(filePath).Split(separator);

            var seq = short.Parse(fileNameParts[0]);
            var subSeq = short.Parse(fileNameParts[1]);
            var id = short.Parse(fileNameParts[2]);
            var maxZ = 0M; // We don't have maxZ in the filename parts
            var tool = fileNameParts[3];

            var startCoords = fileNameParts[4].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
            var endCoords = fileNameParts[5].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
            var start = new Coord(startCoords[0], startCoords[1]);
            var end = new Coord(endCoords[0], endCoords[1]);

            nodes.Add(new Node(seq, subSeq, id, maxZ, tool, start, end));
        }

        return nodes;
    }

    public static int MergeNodes(this string inputFolder, List<Node> nodes) {
        var mergeFileName = $"{inputFolder}-ts.nc";
        int[] idCounts = [nodes.Select(n => n.Seq).Distinct().Count(), nodes.Select(n => n.SubSeq).Distinct().Count(), nodes.Count];

        var firstNodeFileName = nodes[0].NodeFileName(inputFolder, idCounts);
        var firstNodeInputLines = firstNodeFileName.ReadFileLines();
        var preambleLines = firstNodeInputLines.GetPreamble();
        File.WriteAllLines(mergeFileName, preambleLines);

        var lastLine = new Line("");
        var firstNode = true;
        var expectedFirstTool = nodes[0].Tool;
        var firstTool = false;

        foreach (var node in nodes) {
            var nodeFileName = node.NodeFileName(inputFolder, idCounts);
            var inputLines = nodeFileName.ReadFileLines();
            var travellingComments = inputLines.GetTravellingComments();

            Line lastPlaneSelection = new Line("G17");

            var iL = inputLines.GetEnumerator();
            while (iL.MoveNext()) {
                var line = new Line(iL.Current);
                if (line.HasPlaneSelection()) {
                    lastPlaneSelection = new Line(line);
                }
                if (line.ToString() == Default.PreambleCompleted) {
                    break;
                }
            }

            foreach (var travelling in travellingComments) {
                while (iL.MoveNext()) {
                    var line = new Line(iL.Current);
                    if (line.HasPlaneSelection()) {
                        lastPlaneSelection = new Line(line);
                    }
                    if (firstNode && expectedFirstTool != "notset") {
                        // Test if we need to emit a tool change
                        if (line.HasToken('T')) {
                            firstTool = true;
                        }
                        if (line.HasMovementCommand() && !line.HasToken("G0") && !firstTool) {
                            // Emit a tool change
                            File.AppendAllLines(mergeFileName, [$"T{expectedFirstTool}", "M3"]);
                            firstTool = true;
                        }
                    }

                    if (line != lastLine) {
                        File.AppendAllLines(mergeFileName, [line.ToString()]);
                    }
                    lastLine = new Line("");

                    if (line.ToString().EndsWith(travelling)) {
                        if (lastPlaneSelection.ToString() != "G17") {
                            // Ensure a reversion to XY plane selection
                            File.AppendAllLines(mergeFileName, ["G17"]);
                        }
                        lastLine = new Line(line);
                        lastLine = new Line(lastLine.ToSimpleString());
                        break;
                    }
                }
            }
            firstNode = false;
        }

        var lastNodeFileName = nodes[^1].NodeFileName(inputFolder, idCounts);
        var lastNodeInputLines = lastNodeFileName.ReadFileLines();
        var lastTravellingComments = lastNodeInputLines.GetTravellingComments();
        var postambleLines = lastNodeInputLines.GetPostamble(lastTravellingComments[^1]);
        File.AppendAllLines(mergeFileName, postambleLines);

        // 0 == success
        return 0;
    }
}
