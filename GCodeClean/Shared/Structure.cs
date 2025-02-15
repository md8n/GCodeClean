// Copyright (c) 2023-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

// Structures shared by Split and Merge

using GCodeClean.Structure;

namespace GCodeClean.Shared;

public readonly record struct Node(short Seq, short SubSeq, short Id, decimal MaxZ, string Tool, Coord Start, Coord End);
