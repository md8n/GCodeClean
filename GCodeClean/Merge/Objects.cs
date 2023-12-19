// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using GCodeClean.Structure;

namespace GCodeClean.Merge
{       
    public readonly record struct Node(string Tool, short Id, Coord Start, Coord End);
    public record struct Edge(short PrevId, short NextId, decimal Distance, short Weighting) {
        public Int16 Weighting { get; set; } = Weighting;
    };
}
