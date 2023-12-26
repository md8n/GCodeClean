// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using Spectre.Console;

using GCodeClean.Processing;


namespace GCodeClean.Merge
{
    public static class Utility
    {
        public static Node GetNode(this IEnumerable<Node> nodes, short id) {
            return nodes.First(n => n.Id == id);
        }

        public static Edge? GetEdge(this IEnumerable<Edge> edges, short prevId, short nextId) {
            var foundEdge = edges.FirstOrDefault(n => n.PrevId == prevId && n.NextId == nextId);
            return (foundEdge.PrevId == 0 && foundEdge.NextId == 0 && foundEdge.Distance == 0) ? null : foundEdge;
        }

        public static Edge? GetEdgeByPrevId(this IEnumerable<Edge> edges, short prevId) {
            var foundEdge = edges.FirstOrDefault(n => n.PrevId == prevId);
            return (foundEdge.PrevId == 0 && foundEdge.NextId == 0 && foundEdge.Distance == 0) ? null : foundEdge;
        }

        public static Edge? GetEdgeByNextId(this IEnumerable<Edge> edges, short nextId) {
            var foundEdge = edges.FirstOrDefault(n => n.NextId == nextId);
            return (foundEdge.PrevId == 0 && foundEdge.NextId == 0 && foundEdge.Distance == 0) ? null : foundEdge;
        }

        public static decimal TotalDistance(this List<Node> nodes, List<short> nodeIds) {
            var distance = 0M;
            for (var ix = 0; ix < nodeIds.Count - 1; ix++) {
                var prevNode = nodes.GetNode(nodeIds[ix]);
                var nextNode = nodes.GetNode(nodeIds[ix + 1]);
                distance += (prevNode.End, nextNode.Start).Distance();
            }
            return distance;
        }

        /// <summary>
        /// Converts a list of edges (must be contiguous chain) into a list of node Ids
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        public static List<short> GetNodeIds(this List<Edge> edges) {
            List<short> nodeIds = [edges[0].PrevId];
            nodeIds.AddRange(edges.Select(e => e.NextId));
            return nodeIds;
        }

        /// <summary>
        /// Converts a list of node Ids, into a linked list of edges
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        public static List<Edge> GetEdges(this List<short> nodeIds, List<Edge> edges) {
            List<Edge> nodeListEdges = [];
            for (var jx = 0; jx < nodeIds.Count - 1; jx++) {
                var nlEdge = edges.GetEdge(nodeIds[jx], nodeIds[jx + 1]);
                if (nlEdge == null) {
                    // There's no way this could happen excluding some weird programmer error
                    continue;
                }
                nodeListEdges.Add((Edge)nlEdge);
            }

            return nodeListEdges;
        }

        /// <summary>
        /// Identify primary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes unless a loop is identified
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Edge> GetPrimaryEdges(this List<Node> nodes) {
            AnsiConsole.MarkupLine($"Pass [bold yellow]0[/]: Primary Edges");

            List<Edge> primaryEdges = [];
            foreach (var (tool, id, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.Tool == tool && n.Id != id && n.Start.X == end.X && n.Start.Y == end.Y);
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
            AnsiConsole.MarkupLine($"Pass [bold yellow]{weighting}[/]: Secondary Edges");
#pragma warning disable S2234 // Arguments should be passed in the same order as the method parameters
            // Invert existing pairings, and mark as 'do not use' weighting = 100
            List<Edge> alreadyPaired = pairedEdges.Select(pe => new Edge(pe.NextId, pe.PrevId, pe.Distance, 100)).ToList();
#pragma warning restore S2234 // Arguments should be passed in the same order as the method parameters
            var unpairedPrevNodes = pairedEdges.UnpairedPrevNodes(nodes);
            var unpairedNextNodes = pairedEdges.UnpairedNextNodes(nodes);
            List<Edge> seedPairings = [.. alreadyPaired.BuildTravellingPairs(unpairedPrevNodes, unpairedNextNodes, weighting, 1).Where(sp => sp.Distance == 0)];
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

        private static bool HasProcessableEdges(this List<Edge> edges) {
            return edges.Exists(e => e.Weighting < 100);
        }

        public static List<Node> IntersectNodes(this List<Node> nodes, IEnumerable<Node> otherNodes) {
            return nodes.IntersectBy(otherNodes.Select(on => on.Id), e => e.Id).ToList();
        }

        public static List<Edge> RemoveDuplicates(this IEnumerable<Edge> edges) {
            List<Edge> dedupEdges = [];

            foreach (var edge in edges) {
                if (dedupEdges.Exists(de => de.PrevId == edge.PrevId && de.NextId == edge.NextId)) {
                    continue;
                }
                dedupEdges.Add(edge);
            }

            return dedupEdges;
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
                                popEdge.Weighting = 100;
                                throw new Exception("Need to rotate, dunno how");
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

        public static (List<short> startIds, List<short> endIds) GetStartsAndEnds(this List<Edge> edges) {
            var starts = edges.Where(e => e.Weighting < 100).Select(pe => pe.PrevId).ToList();
            var ends = edges.Where(e => e.Weighting < 100).Select(pe => pe.NextId).ToList();
            // Find the starting node Ids - one for each tool - if the tool is used for more than one cutting path
            return (starts.Where(si => !ends.Contains(si)).ToList(), ends.Where(ei => !starts.Contains(ei)).ToList());
        }
    }
}
