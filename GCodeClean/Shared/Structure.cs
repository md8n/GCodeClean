// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

// Structures shared by Split and Merge

using GCodeClean.Structure;

namespace GCodeClean.Shared
{
    public readonly record struct Node(string Tool, short Id, Coord Start, Coord End);
}
