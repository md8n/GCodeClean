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
    public static class Merge
    {
        private static readonly char[] separator = ['_'];

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
        /// Find all nodes that are not pointed to as the Previous node by an edge
        /// </summary>
        /// <param name="edgePairs"></param>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Node> UnpairedPrevNodes(this List<Edge> edgePairs, List<Node> nodes) {
            return nodes.Where(n => !edgePairs.Exists(ep => ep.PrevId == n.Id)).ToList();
        }

        public static List<Node> UnpairedNextNodes(this List<Edge> edgePairs, List<Node> nodes) {
            return nodes.Where(n => !edgePairs.Exists(ep => ep.NextId == n.Id)).ToList();
        }

        public static List<Edge> BuildTravellingPairs(this List<Edge> knownLoopForkPairs, List<Node> unpairedPrevNodes, List<Node> unpairedNextNodes) {
            List<Edge> travellingPairs = [];
            foreach (var upn in unpairedPrevNodes) {
                // Match other nodes (not self) that use the same tool
                // and not a known loop forming edge pair
                // and take the top 10
                var newPairEdges = unpairedNextNodes
                    .Where(unn => unn.Id != upn.Id
                        && unn.Tool == upn.Tool
                        && !knownLoopForkPairs.Exists(pe => pe.PrevId == upn.Id && pe.NextId == unn.Id)
                    )
                    .Select(unn => new Edge(upn.Id, unn.Id, (upn.End, unn.Start).Distance(), 10))
                    .OrderBy(e => e.Distance)
                    .Take(10);
                // Then remove those where the inverse distance is less
                travellingPairs.AddRange(newPairEdges);
            }

            return travellingPairs;
        }

        public static List<Edge> MergeCycle(this List<Edge> sourceEdges, List<Node> nodes, Int16 minWeighting) {
            var firstPassEdges = sourceEdges.CheckForLoops();
            // Just the edges that we haven't marked as closing a loop 
            var pairedEdges = firstPassEdges
                .Where(pe => pe.Weighting < 100)
                .Select(pe => new Edge(pe.PrevId, pe.NextId, pe.Distance, pe.Weighting))
                .ToList();

            // Fill in the nodes we have marked as closing a loop or making a fork - so we don't repeat them
            List<Edge> knownLoopForkPairs = firstPassEdges
                .Where(pe => pe.Weighting >= 100)
                .Select(pe => new Edge(pe.PrevId, pe.NextId, pe.Distance, pe.Weighting))
                .ToList();
            // And add those that are the inverse of all current pairings - as these would equal loops
#pragma warning disable S2234 // Arguments should be passed in the same order as the method parameters
            knownLoopForkPairs.AddRange(pairedEdges.Select(pe => new Edge(pe.NextId, pe.PrevId, pe.Distance, 100)));
#pragma warning restore S2234 // Arguments should be passed in the same order as the method parameters

            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            var unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes);
            List<Edge> travellingPairs = knownLoopForkPairs.BuildTravellingPairs(unpairedPrevNodes, unpairedNextNodes);

            // Focus on the PrevId of the TravellingPair edges
            List<Edge> tpStartEdgeShortest = [];
            foreach (var nodePrevId in travellingPairs.Select(tp => tp.PrevId).Distinct()) {
                var startEdge = travellingPairs.Where(tp => tp.PrevId == nodePrevId).OrderBy(tp => tp.Distance).First();
                startEdge.Weighting = (short)(minWeighting + 1);
                tpStartEdgeShortest.Add(startEdge);
            }
            tpStartEdgeShortest = tpStartEdgeShortest.FilterEdgePairsWithCurrentPairs(pairedEdges);

            // Focus on the NextId of the TravellingPair edges
            List<Edge> tpEndEdgeShortest = [];
            foreach (var nodeNextId in travellingPairs.Select(tp => tp.NextId).Distinct()) {
                var endEdge = travellingPairs.Where(tp => tp.NextId == nodeNextId).OrderBy(tp => tp.Distance).First();
                endEdge.Weighting = (short)(minWeighting + 1);
                tpEndEdgeShortest.Add(endEdge);
            }
            tpEndEdgeShortest = tpEndEdgeShortest.FilterEdgePairsWithCurrentPairs(pairedEdges);

            var tpEdgeShortest = tpStartEdgeShortest.IntersectEdges(tpEndEdgeShortest);

            if (tpEdgeShortest.Count > 0) {
                pairedEdges = [.. pairedEdges, .. tpEdgeShortest];
            } else {
                pairedEdges = [.. pairedEdges, .. tpStartEdgeShortest, .. tpEndEdgeShortest];
            }
            
            pairedEdges = pairedEdges.CheckForLoops().Where(pe => pe.Weighting < 100).ToList();

            //AnsiConsole.MarkupLine("Pairs:");
            //foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}

            // Find all outstanding nodes, and try to 'inject' them between two paired nodes
            unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes);
            var unpairedNodes = unpairedPrevNodes.Intersect(unpairedNextNodes).ToList();

            // If we have unpaired Prev or Next, but no completely unpaired nodes (likely)
            // then we'll try to 'pop' some unpaired Prev or Next, and see what we can get
            //if (unpairedNodes.Count == 0) {
            //    var poppableEdges = pairedEdges.Where(pe => pe.Weighting > 0);
            //    var unpairedNextNodeIds = unpairedNextNodes.Select(n => n.Id);
            //    var edgePrevToPop = poppableEdges.Where(pe => unpairedNextNodeIds.Contains(pe.PrevId)).OrderByDescending(pe => pe.Distance).First();
            //    pairedEdges.Remove(pairedEdges.GetEdge(edgePrevToPop.PrevId, edgePrevToPop.NextId));
            //    unpairedNodes.Add(nodes.GetNode(edgePrevToPop.PrevId));
            //}

            foreach (var node in unpairedNodes) {
                var injectables = pairedEdges
                    .Where(pe => pe.Weighting > 0 && pe.Weighting < 100)
                    .Select(pe => (pe.PrevId, pe.NextId, pe.Distance, node.Id, injPrevDist: (nodes.GetNode(pe.PrevId).End, node.Start).Distance(), injNextDist: (node.End, nodes.GetNode(pe.NextId).Start).Distance()))
                    .OrderBy(inj => inj.injPrevDist + inj.injNextDist - inj.Distance)
                    .Take(10).ToList();
                var (PrevId, NextId, _, Id, injPrevDist, injNextDist) = injectables.FirstOrDefault();

                var swappedEdge = pairedEdges.GetEdge(PrevId, NextId);
                pairedEdges.Remove(swappedEdge);
                pairedEdges.Add(new Edge(PrevId, Id, injPrevDist, swappedEdge.Weighting));
                pairedEdges.Add(new Edge(Id, NextId, injNextDist, swappedEdge.Weighting));
                pairedEdges = pairedEdges.CheckForLoops().Where(pe => pe.Weighting < 100).ToList();
            }

            return pairedEdges;
        }

        public static void MergeFile(this string inputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                AnsiConsole.MarkupLine($"No such folder found. Nothing to see here, move along.");
                return;
            }

            var nodes = inputFolder.GetNodes().Take(20).ToList();
            var tools = nodes.Select(n => n.Tool).Distinct().ToList();
            //AnsiConsole.MarkupLine("Original nodes:");
            //for (var ix = 0; ix < nodes.Count; ix++) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{nodes[ix].Start.X}, {nodes[ix].Start.Y}, {nodes[ix].End.X}, {nodes[ix].End.Y}[/]");
            //}

            if (tools.Count > 1) {
                AnsiConsole.MarkupLine("[bold red]Currently only one tool per merge is supported[/]");
                return;
            }

            var currentDistance = nodes.TotalDistance(nodes.Select(n => n.Id).ToList());

            var pairedEdges = nodes.GetPrimaryEdges();
            List<short> prevStartIds;
            List<short> prevEndIds;
            List<short> startIds;
            List<short> endIds;
            short minCycles = 0;
            short minWeighting = 0;
            do {
                (startIds, endIds) = pairedEdges.GetStartsAndEnds();
                do {
                    prevStartIds = startIds;
                    prevEndIds = endIds;
                    pairedEdges = pairedEdges.MergeCycle(nodes, minWeighting);
                    (startIds, endIds) = pairedEdges.GetStartsAndEnds();
                } while (
                    !prevStartIds.ToHashSet().SetEquals(startIds) &&
                    !prevEndIds.ToHashSet().SetEquals(endIds) &&
                    pairedEdges.Count < (nodes.Count - tools.Count) &&
                    minWeighting++ < 100
                );
                pairedEdges.DivideAndCheck(nodes);
                if (startIds.Count == 1) {
                    break;
                }
                List<Edge> empty = [];
                List<Edge> travellingPairs = empty.BuildTravellingPairs(nodes.Where(n => endIds.Contains(n.Id)).ToList(), nodes.Where(n => startIds.Contains(n.Id)).ToList());
                pairedEdges = [.. pairedEdges, ..travellingPairs.Select(tp => new Edge(tp.PrevId, tp.NextId, tp.Distance, minWeighting))];
                pairedEdges = pairedEdges.CheckForLoops().Where(pe => pe.Weighting < 100).ToList();

            } while (prevStartIds.Count > 1 && minCycles++ < 10);

            AnsiConsole.MarkupLine($"Pairings that were good:");
            foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
                AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            }

            var newDistance = nodes.TotalDistance(pairedEdges.GetNodeIds());

            AnsiConsole.MarkupLine($"Total distinct tools: {tools.Count}");
            AnsiConsole.MarkupLine($"Total nodes: {nodes.Count}");
            AnsiConsole.MarkupLine($"Total edges: {pairedEdges.Count}");

            AnsiConsole.MarkupLine($"Starting node Ids: {string.Join(',', startIds)}");
            AnsiConsole.MarkupLine($"Ending node Ids: {string.Join(',', endIds)}");

            AnsiConsole.MarkupLine($"Current travelling distance: {currentDistance}");
            AnsiConsole.MarkupLine($"New travelling distance: {newDistance}");
            //            List<(string tool, List<int> nodeIds)> cutList = [];
            //            foreach(var toolStartId in toolStartIds) {
            //                var tool = nodes.First(n => n.Id == toolStartId).Tool;
            //                var pairedEdge = pairedEdges.Find(pe => pe.PrevId == toolStartId);
            //                List<int> nodeIds = [pairedEdge.PrevId];
            //#pragma warning disable S2583
            //#pragma warning disable CS8073
            //                do {
            //                    var nextId = pairedEdge.NextId;
            //                    nodeIds.Add(nextId);
            //                    pairedEdge = pairedEdges.Find(pe => pe.PrevId == nextId);
            //                } while (pairedEdge != null);
            //#pragma warning restore CS8073
            //#pragma warning restore S2583
            //                cutList.Add( (tool, nodeIds) );
            //            }

            //foreach (var pair in primaryPairs) {
            //    AnsiConsole.MarkupLine($"Node primary pairs: [bold yellow]{string.Join(',', pair)}[/]");
            //}
            //AnsiConsole.MarkupLine($"Count primary pairs: [bold yellow]{primaryPairs.Count}[/]");
        }
    }
}
