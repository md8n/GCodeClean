// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;
using GCodeClean.Shared;


namespace GCodeClean.Merge
{       
    public static class Merge
    {
        public static async IAsyncEnumerable<string> MergeFileAsync (this string inputFolder)
        {
            if (!inputFolder.FolderExists())
            {
                yield return "No such folder found. Nothing to see here, move along.";
                yield return "Failure";
                yield break;
            }

            var nodes = inputFolder.GetNodes().ToList();

            //AnsiConsole.MarkupLine($"Nodes:");
            //foreach (var node in nodes.Select(n => (n.Id, n.Start, n.End))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{node}[/]");
            //}

            var currentDistance = nodes.TotalDistance(nodes.Select(n => n.Id).ToList());

            Node? firstNode = null;
            List<Edge> pairedEdges = [];

            await foreach (var (seq, subSeq) in nodes.Select(n => (n.Seq, n.SubSeq)).Distinct().ToAsyncEnumerable()) {
                yield return $"Processing sub-sequence {seq}:{subSeq}";
                var subSeqNodes = nodes.Where(n => n.Seq == seq && n.SubSeq == subSeq).ToList();
                if (subSeqNodes.Count > 1) {
                    // Reorder the subsequence of nodes with respect to themselves
                    var subSeqEdges = subSeqNodes.TravellingReorder();
                    var firstSubSeqNode = nodes.GetNode(subSeqEdges[0].PrevId);
                    if (pairedEdges.Count > 0 || firstNode != null) {
                        // Determine if the subsequence of nodes should be 'rotated' with respect to the preceeding node
                        subSeqEdges = subSeqEdges.MaybeRotate(pairedEdges.LastPairedNode(firstNode, nodes), nodes);
                        // Create a joining edge from the preceeding node to the subsequence of edges
                        var joiningEdge = pairedEdges.JoinEdge(firstNode, firstSubSeqNode, nodes);
                        pairedEdges.Add(joiningEdge);
                    }
                    pairedEdges.AddRange(subSeqEdges);
                } else {
                    // Handle a sub sequence only having one node
                    var firstSubSeqNode = subSeqNodes[0];
                    if (pairedEdges.Count > 0 || firstNode != null) {
                        var joiningEdge = pairedEdges.JoinEdge(firstNode, firstSubSeqNode, nodes);
                        pairedEdges.Add(joiningEdge);
                    } else {
                        // Handle the first sub sequence only having one node
                        firstNode = subSeqNodes[0];
                    }
                }
            }

            //AnsiConsole.MarkupLine($"Pairings that were good:");
            //foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}
            var nodeIdList = pairedEdges.GetNodeIds();
            var newDistance = nodes.TotalDistance(nodeIdList);

            yield return $"Total distinct tools: {nodes.Select(n => n.Tool).Distinct().Count()}";
            yield return $"Total nodes: {nodes.Count}";
            yield return $"Total edges: {pairedEdges.Count}";

            var (startIds, endIds) = pairedEdges.GetStartsAndEnds();
            yield return $"Starting node Id: {string.Join(',', startIds)}";
            yield return $"Ending node Id: {string.Join(',', endIds)}";

            yield return $"Current travelling distance: {currentDistance}";
            yield return $"New travelling distance: {newDistance}";

            yield return "Commencing file merge";

            int mergeResult = inputFolder.MergeNodes(pairedEdges.GetNodes(nodes));
            yield return mergeResult == 0 ? "Merge Success" : "Merge Failure";

            yield return "Completed file merge";
        }

        private static List<Edge> MaybeRotate(this List<Edge> subSeqEdges, Node prevNode, List<Node> nodes) {
            // Make a decision about rotating the whole list
            var firstNode = nodes.GetNode(subSeqEdges[0].PrevId);
            var lastNode = nodes.GetNode(subSeqEdges[^1].NextId);
            var (prevId, distance) = subSeqEdges.Select(sse => (prevId: sse.PrevId, distance: (prevNode.End, nodes.GetNode(sse.PrevId).Start).Distance())).OrderBy(se => se.distance).First();
            var maxWeighting = subSeqEdges.Where(sse => sse.Weighting < 100).Select(sse => sse.Weighting).Max();
            var lastToFirstEdge = new Edge(lastNode.Id, firstNode.Id, (lastNode.End, firstNode.Start).Distance(), maxWeighting);
            if (lastToFirstEdge.Distance < distance) {
                var maxEdgeIx = subSeqEdges.FindIndex(sse => sse.PrevId == prevId);
                subSeqEdges = [.. subSeqEdges[(maxEdgeIx + 1)..], lastToFirstEdge, .. subSeqEdges[0..maxEdgeIx]];
            }

            return subSeqEdges;
        }

        private static Edge JoinEdge(this List<Edge> pairedEdges, Node? firstNode, Node firstSubSeqNode, List<Node> nodes) {
            var lastPairedNode = pairedEdges.LastPairedNode(firstNode, nodes);
            short weighting = pairedEdges.Count > 0 ? pairedEdges[^1].Weighting : (short)20;
            return new Edge(lastPairedNode.Id, firstSubSeqNode.Id, (lastPairedNode.End, firstSubSeqNode.Start).Distance(), weighting);
        }

        private static Node LastPairedNode(this List<Edge> pairedEdges, Node? firstNode, List<Node> nodes) {
            return (Node)(firstNode != null ? firstNode : nodes.GetNode(pairedEdges[^1].NextId));
        }
    }
}
