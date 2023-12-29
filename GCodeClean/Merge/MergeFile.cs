// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Linq;


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
            var tools = nodes.Select(n => n.Tool).Distinct().ToList();

            if (tools.Count > 1) {
                Console.WriteLine("Currently only one tool per merge is supported");
                return;
            }

            //AnsiConsole.MarkupLine($"Nodes:");
            //foreach (var node in nodes.Select(n => (n.Id, n.Start, n.End))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{node}[/]");
            //}

            var currentDistance = nodes.TotalDistance(nodes.Select(n => n.Id).ToList());

            var pairedEdges = nodes.TravellingReorder();

            //AnsiConsole.MarkupLine($"Pairings that were good:");
            //foreach (var pair in pairedEdges.Select(tps => (tps.PrevId, tps.NextId, tps.Distance, tps.Weighting))) {
            //    AnsiConsole.MarkupLine($"[bold yellow]{pair}[/]");
            //}
            var nodeIdList = pairedEdges.GetNodeIds();
            var newDistance = nodes.TotalDistance(nodeIdList);

            Console.WriteLine($"Total distinct tools: {tools.Count}");
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
