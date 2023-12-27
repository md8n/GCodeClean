// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Spectre.Console;

using GCodeClean.Processing;


namespace GCodeClean.Merge
{
    public static class NodesAndEdges
    {
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
        /// Converts a list of edges (must be contiguous chain) into a list of nodes
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        public static List<Node> GetNodes(this List<Edge> edges, List<Node> currentNodes) {
            List<short> nodeIds = edges.GetNodeIds();
            List<Node> newNodes = [];
            foreach(var nodeId in nodeIds) {
                newNodes.Add(currentNodes.GetNode(nodeId));
            }
            return newNodes;
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
    }
}
