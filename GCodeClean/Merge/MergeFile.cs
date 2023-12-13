// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Structure;

using Spectre.Console;

namespace GCodeClean.Merge
{
    public static partial class Merge
    {
        private static readonly char[] separator = ['_'];

        public readonly record struct Node(string Tool, Int16 Id, Coord Start, Coord End);
        public record struct Edge(Int16 PrevId, Int16 NextId, decimal Distance, Int16 Weighting) {
            public Int16 Weighting { get; set; } = Weighting;
        };

        public static string ToSimpleString(this Edge edge) => $"{edge.PrevId}<->{edge.NextId}";

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

        /// <summary>
        /// Identify primary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes unless a loop is identified
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static List<Edge> GetPrimaryEdges(this List<Node> nodes) {
            List<Edge> primaryEdges = [];
            foreach (var (tool, id, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.Tool == tool && n.Id != id && n.Start.X == end.X && n.Start.Y == end.Y);
                if (matchingNodes.Count == 1) {
                    primaryEdges.Add(new Edge(id, matchingNodes[0].Id, 0M, 0));
                }
            }

            return primaryEdges;
        }

        private static List<Edge> CheckForLoops(this List<Edge> edges) {
            List<List<int>> nodeLists = [];
            for(var ix = 0; ix < edges.Count; ix++) {
                var edge = edges[ix];
                var matchingNodeLists = nodeLists.Where(nl => nl[^1] == edge.PrevId).ToList();
                if (matchingNodeLists.Count == 0) {
                    matchingNodeLists.Add([edge.PrevId, edge.NextId]);
                    continue;
                }
                if (matchingNodeLists.Count == 1) {
                    if (matchingNodeLists[0][0] == edge.NextId) {
                        // Loop detected
                        edge.Weighting = 100; // Do not use this
                        continue;
                    }
                    matchingNodeLists[0].Add(edge.NextId);
                }
                if (matchingNodeLists.Count > 1) {
                    throw new ArgumentOutOfRangeException("edges", "How did you get two chains of edges with the same end node ID?");
                }
            }

            return edges;
        }

        /// <summary>
        /// Identify primary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static List<Edge> GetPrimaryPairings(this List<Node> nodes)
        {
            List<Edge> primaryPairings = [];
            foreach (var (tool, id, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.Tool == tool && n.Id != id && n.Start.X == end.X && n.Start.Y == end.Y);
                if (matchingNodes.Count == 1) {
                    primaryPairings.Add(new Edge(id, matchingNodes[0].Id, 0M, 0));
                }
            }

            return primaryPairings;
        }

        public static List<Int16> EndPairedNodeIds(this List<Edge> edgePairs, int minWeighting) {
            return edgePairs.Where(ep => ep.Weighting <= minWeighting).Select(ep => ep.PrevId).ToList();
        }

        public static List<Int16> StartPairedNodeIds(this List<Edge> edgePairs, int minWeighting) {
            return edgePairs.Where(ep => ep.Weighting <= minWeighting).Select(ep => ep.NextId).ToList();
        }

        public static List<Edge> MergeCycle(this List<Edge> sourceEdges, List<Node> nodes, Int16 minWeighting) {
            var firstPassEdges = sourceEdges.CheckForLoops();
            // Just the edges that we haven't marked as closing a loop 
            var pairedEdges = firstPassEdges
                .Where(pe => pe.Weighting < 100)
                .Select(pe => new Edge(pe.PrevId, pe.NextId, pe.Distance, pe.Weighting))
                .ToList();

            // Fill in the nodes we have marked as closing a loop - so we don't repeat them
            List<Edge> knownLoopPairs = firstPassEdges
                .Where(pe => pe.Weighting >= 100)
                .Select(pe => new Edge(pe.PrevId, pe.NextId, pe.Distance, pe.Weighting))
                .ToList();

            // And make sure we do not include anything we already consider as good enough
            var pairedEndNodeIds = pairedEdges.EndPairedNodeIds(minWeighting);
            var pairedStartNodeIds = pairedEdges.StartPairedNodeIds(minWeighting);
            var pairedNodeIds = pairedEndNodeIds.Intersect(pairedStartNodeIds).ToList();
            var nfpNodes = nodes.Where(n => !pairedNodeIds.Contains(n.Id));
            List<Edge> travellingPairs = [];
            foreach (var (tool, id, start, end) in nfpNodes) {
                if (pairedEndNodeIds.Contains(id)) {
                    continue;
                }
                var newPairEdges = from etNode in nfpNodes
                    .Where(nfpn => nfpn.Tool == tool && nfpn.Id != id && !pairedStartNodeIds.Contains(nfpn.Id))
                    .Where(nfpn => !pairedEdges.Exists(pe => pe.PrevId == nfpn.Id && pe.NextId == id))
                    .Where(nfpn => !knownLoopPairs.Exists(pe => pe.PrevId == id && pe.NextId == nfpn.Id))
                    select (new Edge(id, etNode.Id, (end, etNode.Start).Distance(), 10));
                // Take the 10 closest matches for each node
                travellingPairs.AddRange(newPairEdges.OrderBy(tp => tp.Distance).Take(10));
            }
            List<Edge> tpStartEdgeShortest = [];
            foreach (var nodePrevId in travellingPairs.Select(tp => tp.PrevId).Distinct()) {
                var startEdge = travellingPairs.Where(tp => tp.PrevId == nodePrevId).OrderBy(tp => tp.Distance).First();
                startEdge.Weighting = (short)(minWeighting + 1);
                tpStartEdgeShortest.Add(startEdge);
            }
            List<Edge> tpEndEdgeShortest = [];
            foreach (var nodeNextId in travellingPairs.Select(tp => tp.NextId).Distinct()) {
                var endEdge = travellingPairs.Where(tp => tp.NextId == nodeNextId).OrderBy(tp => tp.Distance).First();
                endEdge.Weighting = (short)(minWeighting + 1);
                tpEndEdgeShortest.Add(endEdge);
            }
            List<Edge> tpEdgeShortest = [];
            foreach (var edge in tpStartEdgeShortest.OrderBy(tpsns => tpsns.PrevId)) {
                var matchEdges = tpEndEdgeShortest.Where(tp => tp.PrevId == edge.PrevId && tp.NextId == edge.NextId && tp.Distance == edge.Distance);
                if (matchEdges.Any()) {
                    tpEdgeShortest.Add(edge);
                }
            }
            return [.. pairedEdges, .. tpEdgeShortest];
        }

        public static void MergeFile(this string inputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                AnsiConsole.MarkupLine($"No such folder found. Nothing to see here, move along.");
                return;
            }

            var nodes = inputFolder.GetNodes();
            var tools = nodes.Select(n => n.Tool).Distinct().ToList();
            var primaryEdges = nodes.GetPrimaryEdges();

            List<Edge> pairedEdges;

            Int16 minWeighting = 0;
            do {
                pairedEdges = primaryEdges.MergeCycle(nodes, minWeighting);
                primaryEdges = pairedEdges;
            } while (pairedEdges.Count < (nodes.Count - tools.Count) && minWeighting++ < 10);

            AnsiConsole.MarkupLine($"Pairings that were good:");
            foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting)).OrderBy(tps => tps.PrevId)) {
                AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            }

            AnsiConsole.MarkupLine($"Total distinct tools: {tools.Count}");
            AnsiConsole.MarkupLine($"Total nodes: {nodes.Count}");
            AnsiConsole.MarkupLine($"Total edges: {pairedEdges.Count}");


            //foreach (var pair in primaryPairs) {
            //    AnsiConsole.MarkupLine($"Node primary pairs: [bold yellow]{string.Join(',', pair)}[/]");
            //}
            //AnsiConsole.MarkupLine($"Count primary pairs: [bold yellow]{primaryPairs.Count}[/]");
        }
    }
}
