// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

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

        public static Edge GetEdge(this IEnumerable<Edge> edges, short prevId, short nextId) {
            return edges.First(n => n.PrevId == prevId && n.NextId == nextId);
        }

        public static decimal TotalDistance(this List<Node> nodes, List<short> nodeIds) {
            var distance = 0M;
            for (var ix = 0; ix < nodeIds.Count - 1; ix++) {
                var prevNode = nodes[nodeIds[ix]];
                var nextNode = nodes[nodeIds[ix + 1]];
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
        /// Identify primary pairings of cutting paths, where the end of one cutting path is the same as the start of one other cutting path.
        /// These pairings will not be changed in future passes unless a loop is identified
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        public static List<Edge> GetPrimaryEdges(this List<Node> nodes) {
            List<Edge> primaryEdges = [];
            foreach (var (tool, id, start, end) in nodes) {
                var matchingNodes = nodes.FindAll(n => n.Tool == tool && n.Id != id && n.Start.X == end.X && n.Start.Y == end.Y);
                if (matchingNodes.Count == 1) {
                    primaryEdges.Add(new Edge(id, matchingNodes[0].Id, 0M, 0));
                }
            }

            return primaryEdges;
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
                    if (nodeList[0..^1].Contains(edge.PrevId)) {
                        // Fork detected - within a nodelist
                        edge.Weighting = 100; // Do not use this
                        testEdges[ix] = edge;
                    }
                    if (nodeList[1..].Contains(edge.NextId)) {
                        // Fork detected - within a nodelist
                        edge.Weighting = 100; // Do not use this
                        testEdges[ix] = edge;
                    }
                }

                var matchingNodeListPreceeding = nodeLists.Find(nl => nl[^1] == edge.PrevId);
                var matchingNodeListSucceeding = nodeLists.Find(nl => nl[0] == edge.NextId);

                if (matchingNodeListPreceeding == null && matchingNodeListSucceeding == null) {
                    nodeLists.Add([edge.PrevId, edge.NextId]);
                    continue;
                }

                if (matchingNodeListPreceeding != null && matchingNodeListPreceeding.Contains(edge.NextId)) {
                    // Loop detected
                    edge.Weighting = 100; // Do not use this
                    testEdges[ix] = edge;
                }
                if (matchingNodeListSucceeding != null && matchingNodeListSucceeding.Contains(edge.PrevId)) {
                    // Loop detected
                    edge.Weighting = 100; // Do not use this
                    testEdges[ix] = edge;
                }

                if (matchingNodeListPreceeding != null && matchingNodeListSucceeding != null && edge.Weighting < 100) {
                    // Merge two nodelists into one - unless its a loop
                    matchingNodeListPreceeding.AddRange(matchingNodeListSucceeding);
                    nodeLists.Remove(matchingNodeListSucceeding);
                    continue;
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
