// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

using GCodeClean.Processing;


namespace GCodeClean.Merge
{       
    public static class Merge
    {
        public static void MergeFile(this string inputFolder)
        {
            if (!inputFolder.FolderExists())
            {
                Console.WriteLine("No such folder found. Nothing to see here, move along.");
                return;
            }

            var nodes = inputFolder.GetNodes().ToList();

            //AnsiConsole.MarkupLine($"Nodes:");
            //foreach (var node in nodes.Select(n => (n.Id, n.Start, n.End))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{node}[/]");
            //}

            var currentDistance = nodes.TotalDistance(nodes.Select(n => n.Id).ToList());

            List<Edge> pairedEdges = [];

            foreach (var (seq, subSeq) in nodes.Select(n => (n.Seq, n.SubSeq)).Distinct()) {
                var subSeqNodes = nodes.Where(n => n.Seq == seq && n.SubSeq == subSeq).ToList();
                if (subSeqNodes.Count > 1) {
                    var subSeqEdges = subSeqNodes.TravellingReorder();
                    if (pairedEdges.Count > 0) {
                        var lastPairedNode = nodes.GetNode(pairedEdges[^1].NextId);
                        var firstSubSeqNode = nodes.GetNode(subSeqEdges[0].PrevId);
                        var joiningEdge = new Edge(lastPairedNode.Id, firstSubSeqNode.Id, (lastPairedNode.End, firstSubSeqNode.Start).Distance(), pairedEdges[^1].Weighting);
                        pairedEdges.Add(joiningEdge);
                    }
                    pairedEdges.AddRange(subSeqEdges);
                } else {
                    if (pairedEdges.Count > 0) {
                        var lastPairedNode = nodes.GetNode(pairedEdges[^1].NextId);
                        var firstSubSeqNode = subSeqNodes[0];
                        var joiningEdge = new Edge(lastPairedNode.Id, firstSubSeqNode.Id, (lastPairedNode.End, firstSubSeqNode.Start).Distance(), pairedEdges[^1].Weighting);
                        pairedEdges.Add(joiningEdge);
                    }
                }
            }

            //AnsiConsole.MarkupLine($"Pairings that were good:");
            //foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}
            var nodeIdList = pairedEdges.GetNodeIds();
            var newDistance = nodes.TotalDistance(nodeIdList);

            Console.WriteLine($"Total distinct tools: {nodes.Select(n => n.Tool).Distinct().Count()}");
            Console.WriteLine($"Total nodes: {nodes.Count}");
            Console.WriteLine($"Total edges: {pairedEdges.Count}");

            var (startIds, endIds) = pairedEdges.GetStartsAndEnds();
            Console.WriteLine($"Starting node Id: {string.Join(',', startIds)}");
            Console.WriteLine($"Ending node Id: {string.Join(',', endIds)}");

            Console.WriteLine($"Current travelling distance: {currentDistance}");
            Console.WriteLine($"New travelling distance: {newDistance}");

            inputFolder.MergeNodes(pairedEdges.GetNodes(nodes));
        }
    }
}
