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

        public static List<Edge> BuildTravellingPairs(this List<Edge> knownLoopForkPairs, List<Node> unpairedPrevNodes, List<Node> unpairedNextNodes, short weighting, int topCount = 10) {
            List<Edge> travellingPairs = [];
            foreach (var upn in unpairedPrevNodes) {
                // Match other nodes (not self) that use the same tool
                // and not a known loop forming edge pair
                // and take the top 'count' of the results (default 10)
                var newPairEdges = unpairedNextNodes
                    .Where(unn => unn.Id != upn.Id
                        && unn.Tool == upn.Tool
                        && !knownLoopForkPairs.Exists(pe => pe.PrevId == upn.Id && pe.NextId == unn.Id)
                    )
                    .Select(unn => new Edge(upn.Id, unn.Id, (upn.End, unn.Start).Distance(), weighting))
                    .OrderBy(e => e.Distance)
                    .Take(topCount);
                travellingPairs.AddRange(newPairEdges);
            }

            return travellingPairs;
        }

        public static List<Edge> GetInjectablePairings(this List<Edge> pairedEdges, List<Edge> seedPairings, List<Node> nodes, List<Node> unpairedNodes) {
            List<Edge> injPairings = [];
            for (var ix = 0; ix < seedPairings.Count; ix++) {
                var seedPairing = seedPairings[ix];
                if (seedPairing.Weighting >= 100) {
                    continue;
                }
                var prevNode = nodes.GetNode(seedPairing.PrevId);
                var nextNode = nodes.GetNode(seedPairing.NextId);
                var fun = unpairedNodes.Where(unn => unn.Id != prevNode.Id && unn.Id != nextNode.Id);
                var altPrevEdges = fun
                    .Select(unn => new Edge(prevNode.Id, unn.Id, (prevNode.End, unn.Start).Distance(), 10))
                    .OrderBy(e => e.NextId)
                    .Take(10)
                    .ToList();
                var altNextEdges = fun
                    .Select(upn => new Edge(upn.Id, nextNode.Id, (upn.End, nextNode.Start).Distance(), 10))
                    .OrderBy(e => e.PrevId)
                    .Take(10)
                    .ToList();
                if (altPrevEdges.Count == 0 || altNextEdges.Count == 0) {
                    continue;
                }
                List<(Edge ap, Edge an, decimal distance)> altInjEdges = [];
                foreach (var ap in altPrevEdges) {
                    var an = altNextEdges.Find(an => an.PrevId == ap.NextId);
                    if (an.PrevId == 0 && an.NextId == 0 && an.Distance == 0) {
                        continue;
                    }
                    altInjEdges.Add((ap, an, ap.Distance + an.Distance));
                }
                altInjEdges = [.. altInjEdges.OrderBy(a => a.distance)];
                var triplet = altInjEdges[0];
                if (triplet.distance - seedPairing.Distance < seedPairing.Distance) {
                    List<Edge> tripPair = [triplet.ap, triplet.an];
                    tripPair = tripPair.FilterEdgePairsWithCurrentPairs([.. pairedEdges, .. seedPairings]);
                    if (tripPair.Count == 2) {
                        seedPairing.Weighting = 100;
                        seedPairings[ix] = seedPairing;
                        unpairedNodes.Remove(unpairedNodes.GetNode(triplet.ap.NextId));
                        injPairings.AddRange([triplet.ap, triplet.an]);
                    }
                }
            }
            AnsiConsole.MarkupLine($"Injection Pairings:");
            foreach (var pair in injPairings.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
                AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            }
            return injPairings;
        }

        public static List<Edge> PairSeedingToInjPairings(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
            AnsiConsole.MarkupLine($"Pass [bold yellow]{weighting}[/]: Peer Seeding");
#pragma warning disable S2234 // Arguments should be passed in the same order as the method parameters
            // Invert existing pairings, and mark as 'do not use' weighting = 100
            List<Edge> alreadyPaired = pairedEdges.Select(pe => new Edge(pe.NextId, pe.PrevId, pe.Distance, 100)).ToList();
#pragma warning restore S2234 // Arguments should be passed in the same order as the method parameters
            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            var unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes);
            List<Edge> seedPairings = [.. alreadyPaired.BuildTravellingPairs(unpairedPrevNodes, unpairedNextNodes, weighting, 1).OrderBy(tp => tp.Distance)];
            seedPairings = seedPairings.Where(sp => sp.Weighting < 100).ToList().FilterEdgePairsWithCurrentPairs(pairedEdges);

            if (seedPairings.Count == 0) {
                return pairedEdges;
            }

            if (pairedEdges.Count == 0) {
                // No zero length pairings, so choose the shortest edge pairing that there is, as the seed
                seedPairings = [seedPairings[0]];
            }

            List<Edge> injPairings;
            var unpairedNodes = unpairedPrevNodes.IntersectNodes(unpairedNextNodes);
            injPairings = pairedEdges.GetInjectablePairings(seedPairings, nodes, unpairedNodes);
            pairedEdges = [.. pairedEdges, .. seedPairings, .. injPairings];
            return pairedEdges.CheckForLoops().Where(sp => sp.Weighting < 100).ToList();
        }

        public static List<Edge> BuildResidualPairs(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
            AnsiConsole.MarkupLine($"Pass [bold yellow]{weighting}[/]: Residual pairs");
            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes).Select(n => n.Id);
            var unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes).Select(n => n.Id);

            List<Edge> empty = [];
            List<Edge> residualPairs = empty.BuildTravellingPairs(
                nodes.Where(n => unpairedPrevNodes.Contains(n.Id)).ToList(),
                nodes.Where(n => unpairedNextNodes.Contains(n.Id)).ToList(),
                weighting);

            for (var ix = residualPairs.Count - 1; ix >= 0; ix--) {
                List<Edge> residualPrimary = [residualPairs[ix]];
                List<Edge> residualTest = residualPrimary.FilterEdgePairsWithCurrentPairs(pairedEdges);
                if (residualTest.Count == 0) {
                    residualPairs.Remove(residualPrimary[0]);
                }
            }

            //AnsiConsole.MarkupLine($"Residual Pairings that were good:");
            //foreach (var pair in residualPairs.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}

            List<Edge> finalPairs = [];
            while (residualPairs.Count > 1) {
                var firstNextId = residualPairs.OrderByDescending(rp => rp.NextId).First().NextId;
                var residualPrimary = residualPairs.Where(rp => rp.NextId == firstNextId).OrderBy(rp => rp.Distance).First();
                residualPairs = residualPairs.Where(rp => rp.PrevId != residualPrimary.PrevId && rp.NextId != residualPrimary.NextId).ToList();
                finalPairs.Add(residualPrimary);
            }

            return finalPairs;
        }

        public static void MergeFile(this string inputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                AnsiConsole.MarkupLine($"No such folder found. Nothing to see here, move along.");
                return;
            }

            //var offset = 0;
            //var take = 210;
            var nodes = inputFolder.GetNodes().ToList(); // .Skip(offset).Take(take)
            var tools = nodes.Select(n => n.Tool).Distinct().ToList();

            if (tools.Count > 1) {
                AnsiConsole.MarkupLine("[bold red]Currently only one tool per merge is supported[/]");
                return;
            }

            //AnsiConsole.MarkupLine($"Nodes:");
            //foreach (var node in nodes.Select(n => (n.Id, n.Start, n.End))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{node}[/]");
            //}

            var currentDistance = nodes.TotalDistance(nodes.Select(n => n.Id).ToList());

            var pairedEdges = nodes.GetPrimaryEdges();

            List<short> startIds;
            List<short> endIds;
            int prevCount = 0;
            int postCount = 0;
            short weighting = 1;

            do {
                prevCount = pairedEdges.Count;
                pairedEdges = pairedEdges.GetSecondaryEdges(nodes, weighting++);
                postCount = pairedEdges.Count;
            } while (prevCount < postCount);

            do {
                prevCount = pairedEdges.Count;
                pairedEdges = pairedEdges.PairSeedingToInjPairings(nodes, weighting++);
                postCount = pairedEdges.Count;
            } while (prevCount < postCount);

            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            while (unpairedPrevNodes.Count > 1) {
                weighting++;
                List<Edge> residualPairs = pairedEdges.BuildResidualPairs(nodes, weighting);

                pairedEdges = [.. pairedEdges, .. residualPairs];
                pairedEdges = pairedEdges.CheckForLoops();
                pairedEdges = pairedEdges.Where(pe => pe.Weighting < 100).ToList();

                unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            }

            // Make a final decision about rotating the whole list
            var firstNode = nodes.GetNode(pairedEdges[0].PrevId);
            var lastNode = nodes.GetNode(pairedEdges[^1].NextId);
            var maxEdge = pairedEdges.OrderByDescending(pe => pe.Distance).FirstOrDefault();
            var lastToFirstEdge = new Edge(lastNode.Id, firstNode.Id, (lastNode.End, firstNode.Start).Distance(), weighting);
            if (lastToFirstEdge.Distance < maxEdge.Distance) {
                var maxEdgeIx = pairedEdges.IndexOf(maxEdge);
                pairedEdges = [..pairedEdges[(maxEdgeIx+1)..], lastToFirstEdge, ..pairedEdges[0..maxEdgeIx]];
            }

            //AnsiConsole.MarkupLine($"Pairings that were good:");
            //foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}
            var nodeIdList = pairedEdges.GetNodeIds();
            var newDistance = nodes.TotalDistance(nodeIdList);

            AnsiConsole.MarkupLine($"Total distinct tools: {tools.Count}");
            AnsiConsole.MarkupLine($"Total nodes: {nodes.Count}");
            AnsiConsole.MarkupLine($"Total edges: {pairedEdges.Count}");

            (startIds, endIds) = pairedEdges.GetStartsAndEnds();
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
