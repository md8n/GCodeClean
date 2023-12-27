// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Spectre.Console;


namespace GCodeClean.Merge
{
    public static class Edges
    {
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

        public static bool HasProcessableEdges(this List<Edge> edges) {
            return edges.Exists(e => e.Weighting < 100);
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

        public static (List<short> startIds, List<short> endIds) GetStartsAndEnds(this List<Edge> edges) {
            var starts = edges.Where(e => e.Weighting < 100).Select(pe => pe.PrevId).ToList();
            var ends = edges.Where(e => e.Weighting < 100).Select(pe => pe.NextId).ToList();
            // Find the starting node Ids - one for each tool - if the tool is used for more than one cutting path
            return (starts.Where(si => !ends.Contains(si)).ToList(), ends.Where(ei => !starts.Contains(ei)).ToList());
        }
    }
}
