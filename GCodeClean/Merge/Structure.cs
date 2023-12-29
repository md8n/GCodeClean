// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

namespace GCodeClean.Merge
{
    public record struct Edge(short PrevId, short NextId, decimal Distance, short Weighting) {
        public short Weighting { get; set; } = Weighting;
    };
}
