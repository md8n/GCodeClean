// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Shared;


namespace GCodeClean.Merge
{
    public static class Utility
    {
        /// <summary>
        /// 'Injects' unpaired nodes within existing edges
        /// </summary>
        /// <remarks>Not found to be useful with the way the rest of this algorithm works</remarks>
        /// <param name="pairedEdges"></param>
        /// <param name="seedPairings"></param>
        /// <param name="nodes"></param>
        /// <param name="unpairedNodes"></param>
        /// <returns></returns>
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
            Console.WriteLine("Injection Pairings:");
            foreach (var pair in injPairings.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
                Console.WriteLine($"{pair}");
            }
            return injPairings;
        }
    }
}
