// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Shared;


namespace GCodeClean.Merge
{
    public static class Nodes
    {
        public static Node GetNode(this IEnumerable<Node> nodes, short id) {
            return nodes.First(n => n.Id == id);
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

        public static List<Node> IntersectNodes(this List<Node> nodes, IEnumerable<Node> otherNodes) {
            return nodes.IntersectBy(otherNodes.Select(on => on.Id), e => e.Id).ToList();
        }
    }
}
