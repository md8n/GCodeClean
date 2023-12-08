// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GCodeClean.Structure;

using Spectre.Console;

namespace GCodeClean.Processing
{
    public static partial class Merge {
        private static readonly char[] separator = ['_'];

        /// <summary>
        /// Scan through the file for 'travelling' comments and build a list of them
        /// </summary>
        /// <param name="inputLines"></param>
        /// <returns></returns>
        public static List<(int id, Coord start, Coord end)> GetNodes(this string inputFolder) {
            var fileEntries = Directory.GetFiles(inputFolder);
            Array.Sort(fileEntries);
            List<(int id, Coord start, Coord end)> nodes = [];
            foreach (var filePath in fileEntries) {
                string[] fileNameParts = Path.GetFileNameWithoutExtension(filePath).Split(separator);
                var id = int.Parse(fileNameParts[0]);
                var startCoords = fileNameParts[1].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
                var endCoords = fileNameParts[2].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
                var start = new Coord(startCoords[0], startCoords[1]);
                var end = new Coord(endCoords[0], endCoords[1]);
                nodes.Add((id, start, end));
            }

            return nodes;
        }

        public static void MergeFile(this string inputFolder) {
            if (!Directory.Exists(inputFolder)) {
                AnsiConsole.MarkupLine($"No such folder found. Nothing to see here, move along.");
                return;
            }

            var nodes = GetNodes(inputFolder);
            List<(int idA, int idB)> primaryPairs = [];

            foreach (var (id, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.start.X == end.X && n.start.Y == end.Y);
                if (matchingNodes.Count == 1) {
                    primaryPairs.Add((id, matchingNodes[0].id));
                }
            }

            foreach (var pair in primaryPairs) {
                AnsiConsole.MarkupLine($"Node primary pairs: [bold yellow]{pair}[/]");
            }
            AnsiConsole.MarkupLine($"Count primary pairs: [bold yellow]{primaryPairs.Count}[/]");
        }
    }
}
