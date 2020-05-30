// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Default {
        public static Context Preamble()
        {
            var context = new Context(
                new List<(Line line, bool isOutput)>
                {
                    (new Line("G21"), false), // Length units, mm - alternate G20
                    (new Line("G90"), false), // Distance mode, absolute - alternate G91
                    (new Line("G94"), false), // Feed mode, per minute - alternate G93
                    (new Line("G17"), false), // Set plane, XY - alternates G18, G19
                    (new Line("G40"), false), // Cutter radius compensation, off - alternates are G41, G42
                    (new Line("G49"), false), // Tool length offset, none - alternate G43
                    // (new Line("G61"), false), // Path control mode, exact path - alternates are G61.1, G64
                    // (new Line("G80"), false), // Modal motion (AKA Canned Cycle), Cancel - alternates are G81, G82, G83, G84, G85, G86, G87, G88, G89
                    // (new Line("F"), false), // Feed rate, default will depend on length units
                    // (new Line("S"), false), // Spindle speed
                    // (new Line("T"), false), // Select tool
                    // (new Line("M6"), false), // Change tool
                    // (new Line("M3"), false), // Spindle control, clockwise - alternates are M4, M5
                    // (new Line("M7 M8"), false), // Coolant control, mist and flood - alternates are any one of M7, M8, M9 
                }
            );

            return context;
        }
    }
}
