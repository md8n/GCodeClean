// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Linq;

using Spectre.Console;


namespace GCodeClean.Merge
{       
    public static class Merge
    {
        public static void MergeFile(this string inputFolder)
        {
            if (!inputFolder.FolderExists())
            {
                AnsiConsole.MarkupLine($"No such folder found. Nothing to see here, move along.");
                return;
            }

            var nodes = inputFolder.GetNodes().ToList();
            var tools = nodes.Select(n => n.Tool).Distinct().ToList();

            if (tools.Count > 1) {
                AnsiConsole.MarkupLine("[bold red]Currently only one tool per merge is supported[/]");
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

            AnsiConsole.MarkupLine($"Total distinct tools: {tools.Count}");
            AnsiConsole.MarkupLine($"Total nodes: {nodes.Count}");
            AnsiConsole.MarkupLine($"Total edges: {pairedEdges.Count}");

            var (startIds, endIds) = pairedEdges.GetStartsAndEnds();
            AnsiConsole.MarkupLine($"Starting node Id: {string.Join(',', startIds)}");
            AnsiConsole.MarkupLine($"Ending node Id: {string.Join(',', endIds)}");

            AnsiConsole.MarkupLine($"Current travelling distance: {currentDistance}");
            AnsiConsole.MarkupLine($"New travelling distance: {newDistance}");

            inputFolder.MergeNodes(pairedEdges.GetNodes(nodes));

            //            List<(string tool, List<int> nodeIds)> cutList = [];
            //            foreach(var toolStartId in toolStartIds) {
            //                var tool = nodes.First(n => n.Id == toolStartId).Tool;
            //                var pairedEdge = pairedEdges.Find(pe => pe.PrevId == toolStartId);
            //                List<int> nodeIds = [pairedEdge.PrevId];
            //#pragma warning disable S2583
            //#pragma warning disable CS8073
            //                do {
            //                    var nextId = pairedEdge.NextId;
            //                    nodeIds.Add(nextId);
            //                    pairedEdge = pairedEdges.Find(pe => pe.PrevId == nextId);
            //                } while (pairedEdge != null);
            //#pragma warning restore CS8073
            //#pragma warning restore S2583
            //                cutList.Add( (tool, nodeIds) );
            //            }

            //foreach (var pair in primaryPairs) {
            //    AnsiConsole.MarkupLine($"Node primary pairs: [bold yellow]{string.Join(',', pair)}[/]");
            //}
            //AnsiConsole.MarkupLine($"Count primary pairs: [bold yellow]{primaryPairs.Count}[/]");
        }
    }
}
