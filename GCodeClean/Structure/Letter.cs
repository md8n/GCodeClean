// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

namespace GCodeClean.Structure
{
    public static class Letter
    {
        public static readonly char fileTerminator = '%';

        /// <summary>
        /// Marks the rest of the line as something to be 'ignored' if the "block delete" switch is on
        /// </summary>
        public static readonly char blockDelete = '/';

        public static readonly char commentStart = '(';
        public static readonly char commentEnd = ')';
        public static readonly char commentSemi = ';';

        public static readonly char gCommand = 'G';
        public static readonly char mCommand = 'M';

        public static readonly char feedRate = 'F';
        public static readonly char spindleSpeed = 'S';
        public static readonly char selectTool = 'T';

        public static readonly char lineNumber = 'N';

        public static readonly char[] FileTerminators = [fileTerminator];
        public static readonly char[] BlockDeletes = [blockDelete];
        public static readonly char[] Comments = [commentStart, commentSemi];
        public static readonly char[] Commands = [gCommand, mCommand];
        public static readonly char[] Codes = [feedRate, spindleSpeed, selectTool];
        public static readonly char[] Arguments = ['A', 'B', 'C', 'D', 'H', 'I', 'J', 'K', 'L', 'P', 'R', 'X', 'Y', 'Z'];
        public static readonly char[] LineNumbers = [lineNumber];
        /// <summary>
        /// Parameters are identified by a Hash followed by an integer (from 1 to 5399)
        /// Parameters may be set (a command) or may be used as a value (after a command, code or argument)
        /// </summary>
        public static readonly char[] Parameters = ['#'];
        public static readonly char[] Other = ['E', 'O', 'Q', 'U', 'V'];

        public static readonly decimal[] GCodes = [
            0, 1, 2, 3, 4, 10, 17, 18, 19, 20, 21, 28, 30, 38.2M,
            40, 41, 42, 43, 49, 53, 54, 55, 56, 57, 58, 59, 59.1M, 59.2M, 59.3M,
            61, 61.1M, 64, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89,
            90, 91, 92, 92.1M, 92.2M, 92.3M, 93, 94, 98, 99
        ];

        public static readonly decimal[] MCodes = [
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 30, 48, 49, 60
        ];
    }
}
