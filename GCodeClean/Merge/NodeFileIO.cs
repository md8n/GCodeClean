// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GCodeClean.Structure;

using Spectre.Console;

namespace GCodeClean.Merge
{       
    public static class NodeFileIO
    {
        private static readonly char[] separator = ['_'];

        public static string ToSimpleString(this Edge edge) => $"{edge.PrevId}<->{edge.NextId}";

        public static bool FolderExists(this string inputFolder) {
            return Directory.Exists(inputFolder);
        }

        /// <summary>
        /// Scan through the file for 'travelling' comments and build a list of them
        /// </summary>
        /// <param name="inputLines"></param>
        /// <returns></returns>
        public static List<Node> GetNodes(this string inputFolder) {
            var fileEntries = Directory.GetFiles(inputFolder);
            Array.Sort(fileEntries);
            List<Node> nodes = [];
            foreach (var filePath in fileEntries) {
                var fileNameParts = Path.GetFileNameWithoutExtension(filePath).Split(separator);
                var tool = fileNameParts[0];
                var id = Int16.Parse(fileNameParts[1]);
                var startCoords = fileNameParts[2].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
                var endCoords = fileNameParts[3].Replace("X", "").Split("Y").Select(c => decimal.Parse(c)).ToArray();
                var start = new Coord(startCoords[0], startCoords[1]);
                var end = new Coord(endCoords[0], endCoords[1]);
                nodes.Add(new Node(tool, id, start, end));
            }

            return nodes;
        }
    }
}
