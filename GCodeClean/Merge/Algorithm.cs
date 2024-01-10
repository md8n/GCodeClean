// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Shared;

namespace GCodeClean.Merge
{
    public static class Algorithm
    {
        /// <summary>
        /// Identify primary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes unless a loop is identified
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Edge> GetPrimaryEdges(this List<Node> nodes) {
            Console.WriteLine("Pass 0: Primary Edges");

            List<Edge> primaryEdges = [];
            foreach (var (seq, subSeq, id, maxZ, tool, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.Seq == seq && n.SubSeq == subSeq && n.Tool == tool && n.Id != id && n.Start.X == end.X && n.Start.Y == end.Y);
                if (matchingNodes.Count > 1) {
                    // This may be some kind of 'peck-drilling' operation, whatever it is
                    // simply take the first node
                    // where the start and end are the same
                    matchingNodes = matchingNodes.Where(mn => mn.Start.X == mn.End.X && mn.Start.Y == mn.End.Y).Take(1).ToList();
                }
                if (matchingNodes.Count == 1 && primaryEdges.GetEdge(matchingNodes[0].Id, id) == null) {
                    primaryEdges.Add(new Edge(id, matchingNodes[0].Id, 0M, 0));
                }
            }

            return primaryEdges;
        }

        /// <summary>
        /// Identify secondary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes unless a loop is identified
        /// </summary>
        /// <param name="pairedEdges"></param>
        /// <param name="nodes"></param>
        /// <param name="weighting"></param>
        /// <returns></returns>
        public static List<Edge> GetSecondaryEdges(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
            Console.WriteLine($"Pass {weighting}: Secondary Edges");
            List<Edge> seedPairings = [.. pairedEdges.GetResidualSeedPairings(nodes, weighting).Where(sp => sp.Distance == 0)];
            return seedPairings.GetFilteredSeedPairings(pairedEdges);
        }

        public static List<Edge> PairSeedingToInjPairings(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
            Console.WriteLine($"Pass {weighting}: Peer Seeding");
            List<Edge> seedPairings = [.. pairedEdges.GetResidualSeedPairings(nodes, weighting).OrderBy(tp => tp.Distance)];

            /* Injecting nodes into other existing edge pairings, not found to be useful */
            //List<Edge> injPairings;
            //var unpairedNodes = unpairedPrevNodes.IntersectNodes(unpairedNextNodes);
            //injPairings = pairedEdges.GetInjectablePairings(seedPairings, nodes, unpairedNodes);
            //pairedEdges = [.. pairedEdges, .. seedPairings, .. injPairings];

            return seedPairings.GetFilteredSeedPairings(pairedEdges);
        }

        public static List<Edge> BuildResidualPairs(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
            Console.WriteLine($"Pass {weighting}: Residual pairs");
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

        public static List<Edge> GetResidualSeedPairings(this List<Edge> pairedEdges, List<Node> nodes, short weighting) {
#pragma warning disable S2234 // Arguments should be passed in the same order as the method parameters
            // Invert existing pairings, and mark as 'do not use' weighting = 100
            List<Edge> alreadyPaired = pairedEdges.Select(pe => new Edge(pe.NextId, pe.PrevId, pe.Distance, 100)).ToList();
#pragma warning restore S2234 // Arguments should be passed in the same order as the method parameters
            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            var unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes);
            List<Edge> seedPairings = [.. alreadyPaired.BuildTravellingPairs(unpairedPrevNodes, unpairedNextNodes, weighting, 1)];

            return seedPairings;
        }

        public static List<Edge> GetFilteredSeedPairings(this List<Edge> seedPairings, List<Edge> pairedEdges) {
            seedPairings = seedPairings.Where(sp => sp.Weighting < 100).ToList().FilterEdgePairsWithCurrentPairs(pairedEdges);

            if (seedPairings.Count == 0) {
                return pairedEdges;
            }

            if (pairedEdges.Count == 0) {
                // No zero length pairings, so choose the shortest edge pairing that there is, as the seed
                seedPairings = [seedPairings[0]];
            }

            pairedEdges = [.. pairedEdges, .. seedPairings];
            return pairedEdges.CheckForLoops().Where(sp => sp.Weighting < 100).ToList();
        }

        /// <summary>
        /// Takes the supplied list of edges and determines if there are any loops or forks
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        public static List<Edge> CheckForLoops(this List<Edge> edges) {
            edges = edges.RemoveDuplicates();
            if (!edges.HasProcessableEdges()) {
                return [];
            }
            List<List<short>> nodeLists = [];

            // Get the max weighting value less than 100, it's the edges with this value that we will sort before checking for loops
            var maxWeighting = edges.Where(e => e.Weighting < 100).OrderByDescending(e => e.Weighting).First().Weighting;

            // Deep copy the edges first before starting, we want them in a specific order, and because the reordering at the end is destructive
            var testEdges = edges.Where(e => e.Weighting < maxWeighting).Select(e => new Edge(e.PrevId, e.NextId, e.Distance, e.Weighting)).ToList();
            testEdges.AddRange(edges.Where(e => e.Weighting == maxWeighting).OrderBy(e => e.Distance).Select(e => new Edge(e.PrevId, e.NextId, e.Distance, e.Weighting)));
            testEdges.AddRange(edges.Where(e => e.Weighting >= 100));

            for (var ix = 0; ix < testEdges.Count; ix++) {
                var edge = testEdges[ix];
                if (edge.Weighting >= 100) {
                    // Already excluded
                    continue;
                }

                foreach (var nodeList in nodeLists) {
                    var hasFork = false;
                    if (nodeList[0..^1].Contains(edge.PrevId)) {
                        hasFork = true;
                    }
                    if (nodeList[1..].Contains(edge.NextId)) {
                        hasFork = true;
                    }
                    if (hasFork) {
                        // Fork detected - within a nodelist
                        List<Edge> nodeListEdges = nodeList.GetEdges(testEdges);
                        // Check for inversions
                        if (nodeListEdges.GetEdge(edge.NextId, edge.PrevId) != null) {
                            // A simple inversion
                            edge.Weighting = 100; // Do not use this
                            testEdges[ix] = edge;
                        } else {
                            var altEdge = (Edge)(nodeListEdges.GetEdgeByPrevId(edge.PrevId) ?? nodeListEdges.GetEdgeByNextId(edge.NextId));
                            if (altEdge.Distance <= edge.Distance) {
                                edge.Weighting = 100; // Do not use this
                                testEdges[ix] = edge;
                            } else {
                                // Make a decision about the fork
                                var altEdgeIx = nodeListEdges.IndexOf(altEdge);
                                if (altEdgeIx != 0 && altEdgeIx != nodeListEdges.Count - 1) {
                                    // If it is not effectively at the start or end of the nodelist, we'll reject it
                                    edge.Weighting = 100; // Do not use this
                                    testEdges[ix] = edge;
                                } else {
                                    var altTestEdge = (Edge)testEdges.GetEdge(altEdge.PrevId, altEdge.NextId);
                                    var altTestEdgeIx = testEdges.IndexOf(altTestEdge);
                                    altTestEdge.Weighting = 100;
                                    testEdges[altTestEdgeIx] = altTestEdge;
                                    nodeListEdges[altEdgeIx] = edge;
                                    if (altEdgeIx == 0) {
                                        nodeList[0] = edge.PrevId;
                                    } else {
                                        nodeList[altEdgeIx] = edge.NextId;
                                    }
                                }
                            }
                        }
                        continue;
                    }
                }

                var matchingNodeListPreceeding = nodeLists.Find(nl => nl[^1] == edge.PrevId);
                var matchingNodeListSucceeding = nodeLists.Find(nl => nl[0] == edge.NextId);

                if (matchingNodeListPreceeding == null && matchingNodeListSucceeding == null) {
                    nodeLists.Add([edge.PrevId, edge.NextId]);
                    continue;
                }

                if (matchingNodeListPreceeding != null) {
                    if (matchingNodeListPreceeding[0] == edge.NextId) {
                        // Loop detected - pop the longest edge
                        // We could alternatively check if preceeding and succeeding are the same nodelist
                        LinkedList<Edge> nodeListEdges = new LinkedList<Edge>(matchingNodeListPreceeding.GetEdges(testEdges));
                        var distances = nodeListEdges.Select(nle => nle.Distance).Distinct().ToList();
                        if (distances.Max() <= edge.Distance) {
                            edge.Weighting = 100; // Do not use this
                            testEdges[ix] = edge;
                        } else {
                            // Find and pop the longest edge
                            nodeListEdges.AddLast(edge);
                            var popEdge = nodeListEdges.OrderByDescending(nle => nle.Distance).First();
                            if (popEdge == edge) {
                                edge.Weighting = 100; // Do not use this
                                testEdges[ix] = edge;
                            } else {
                                // we need to rotate the list
                                while (nodeListEdges.First.Value != popEdge) {
                                    var firstEdge = nodeListEdges.First;
                                    nodeListEdges.RemoveFirst();
                                    nodeListEdges.AddLast(firstEdge);
                                }
                                var popEdgeIx = testEdges.IndexOf(popEdge);
                                popEdge.Weighting = 100;
                                testEdges[popEdgeIx] = popEdge;
                                nodeListEdges.RemoveFirst();
                                var nodeListIx = nodeLists.IndexOf(matchingNodeListPreceeding);
                                nodeLists[nodeListIx] = nodeListEdges.ToList().GetNodeIds();
                            }
                        }
                    } else if (matchingNodeListSucceeding != null && matchingNodeListPreceeding != matchingNodeListSucceeding) {
                        // Merge two nodelists into one - unless its a loop
                        matchingNodeListPreceeding.AddRange(matchingNodeListSucceeding);
                        nodeLists.Remove(matchingNodeListSucceeding);
                        continue;
                    }
                }

                if (matchingNodeListPreceeding != null && !matchingNodeListPreceeding.Contains(edge.NextId)) {
                    matchingNodeListPreceeding.Add(edge.NextId);
                }
                if (matchingNodeListSucceeding != null && !matchingNodeListSucceeding.Contains(edge.PrevId)) {
                    matchingNodeListSucceeding.Insert(matchingNodeListSucceeding.IndexOf(edge.NextId), edge.PrevId);
                }
            }

            List<Edge> orderedEdges = [];
            foreach (List<short> nodeList in nodeLists) {
                for (var ix = 0; ix < nodeList.Count - 1; ix++) {
                    var edge = testEdges.Find(e => e.PrevId == nodeList[ix] && e.NextId == nodeList[ix + 1]);
                    orderedEdges.Add(edge);
                    testEdges.Remove(edge);
                }
            }
            orderedEdges.AddRange(testEdges);

            return orderedEdges;
        }

        /// <summary>
        /// Filter supplied edge pairs for anything with a weighting of 100 or more
        /// </summary>
        /// <param name="edges"></param>
        /// <returns>A new list of filtered edge pairs</returns>
        public static List<Edge> FilterEdgePairs(this List<Edge> edges) {
            if (!edges.HasProcessableEdges()) {
                return [];
            }
            return edges.CheckForLoops()
                .Where(ep => ep.Weighting < 100)
                .Select(ep => new Edge(ep.PrevId, ep.NextId, ep.Distance, ep.Weighting))
                .ToList();
        }

        /// <summary>
        /// Filter the supplied set of edge pairs after combining them with a current set of edge pairs
        /// </summary>
        /// <param name="edges"></param>
        /// <param name="currentPairs"></param>
        /// <returns>A new list of filtered edge pairs</returns>
        public static List<Edge> FilterEdgePairsWithCurrentPairs(this List<Edge> edges, List<Edge> currentPairs) {
            if (!edges.HasProcessableEdges()) {
                return [];
            }
            var firstFilteredEP = edges.FilterEdgePairs();
            List<Edge> tempEdges = [.. currentPairs, .. firstFilteredEP];
            tempEdges = tempEdges.FilterEdgePairs();

            return firstFilteredEP.Where(ff => tempEdges.Exists(te => te.PrevId == ff.PrevId && te.NextId == ff.NextId)).ToList();
        }

        /// <summary>
        /// Reorder the list of nodes to achieve a shorter travelling distance
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Edge> TravellingReorder(this List<Node> nodes)
        {
            var pairedEdges = nodes.GetPrimaryEdges();

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
                List<Edge> residualPairs = pairedEdges.BuildResidualPairs(nodes, weighting++);

                pairedEdges = [.. pairedEdges, .. residualPairs];
                pairedEdges = pairedEdges.CheckForLoops().Where(pe => pe.Weighting < 100).ToList();

                unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            }

            // Make a decision about rotating the whole list
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

            return pairedEdges;
        }
    }
}
