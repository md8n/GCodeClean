// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using GCodeClean.Structure;
using System.Collections.Generic;

namespace GCodeClean.Processing;

public static class Default
{
    /// <summary>
    /// A default preamble for injection before the first movement code.
    /// </summary>
    /// <returns></returns>
    public static Context Preamble()
    {
        var context = new Context(
            [
                (new Line("G21"), false), // Length units, mm - alternate G20
                (new Line("G90"), false), // Distance mode, absolute - alternate G91
                (new Line("G94"), false), // Feed mode, per minute - alternate G93
                (new Line("G17"), false), // Set plane, XY - alternates G18, G19
                (new Line("G40"), false), // Cutter radius compensation, off - alternates are G41, G42
                (new Line("G49"), false), // Tool length offset, none - alternate G43
                (new Line("G54"), false), // Coordinate system selection - alternate G55, G56, G57, G58, G59, G59.1, G59.2, G59.3
                // (new Line("T1"), false), // Select tool 1 (arbitrary default)
                // (new Line("M6"), false), // Change tool
                (new Line("M3"), false), // Spindle control, clockwise - alternates are M4, M5
                // (new Line("G61"), false), // Path control mode, exact path - alternates are G61.1, G64
                // (new Line("G80"), false), // Modal motion (AKA Canned Cycle), Cancel - alternates are G81, G82, G83, G84, G85, G86, G87, G88, G89
                // (new Line("F"), false), // Feed rate, default will depend on length units
                // (new Line("G4 P2"), false), // Dwell, for 2 seconds - useful before any S command to allow the spindle to speed up
                // (new Line("S"), false), // Spindle speed
                // (new Line("M7 M8"), false), // Coolant control, mist and flood - alternates are any one of M7, M8, M9 
            ]
        );

        return context;
    }

    /// <summary>
    /// A default list of all preamble tokens that can appear in a preamble.
    /// It is assumed that these tokens will only be used once in the GCode, but technically they could be used multiple times
    /// </summary>
    /// <returns></returns>
    public static List<Token> PreambleAll()
    {
        List<Token> tokens =
            [
                // Length units
                new Token("G20"),
                new Token("G21"),
                // Distance mode
                new Token("G90"),
                new Token("G91"),
                // Feed mode
                new Token("G93"),
                new Token("G94"),
                // Set plane
                new Token("G17"),
                new Token("G18"),
                new Token("G19"),
                // Cutter radius compensation
                new Token("G40"),
                new Token("G41"),
                new Token("G42"),
                // Tool length offset
                new Token("G43"),
                new Token("G49"),
                // Coordinate system selection
                new Token("G54"),
                new Token("G55"),
                new Token("G56"),
                new Token("G57"),
                new Token("G58"),
                new Token("G59"),
                new Token("G59.1"),
                new Token("G59.2"),
                new Token("G59.3"),
                // Spindle control
                new Token("M3"),
                new Token("M4"),
                new Token("M5"),
                // Path control mode
                new Token("G61"),
                new Token("G61.1"),
                new Token("G64"),
            ];

        // Currently unwanted in preamble context
        //List<Token> unWantedTokens =
        //    [
        //        // Select tool
        //        new Token("T"),
        //        // Change tool
        //        new Token("M6"),
        //        // new Token("G80"), // Modal motion (AKA Canned Cycle), Cancel - alternates are G81, G82, G83, G84, G85, G86, G87, G88, G89
        //        // Feed rate
        //        new Token("F"),
        //        // Spindle speed
        //        new Token("S"),
        //    ];


        // Currently unsupported for preamble context
        //List<Token> unSupportedTokens =
        //    [
        //        // Dwell, (for P(some) seconds) - useful before any S command to allow the spindle to speed up - Not supported yet
        //        new Token("G4 P"),
        //        // Coolant control, mist and flood - alternates are any one of M7, M8, (M7 M8), M9  - Not supported yet
        //        new Token("M7"),
        //        new Token("M8"),
        //        new Token("M9"),
        //    ];

        return tokens;
    }

    /// <summary>
    /// A default postamble for injection after the last movement command and before any program stop
    /// </summary>
    /// <returns></returns>
    public static Context Postamble()
    {
        var context = new Context(
            [
                (new Line("Z10"), false), // Raise the tool - this will need to be zClamped
                (new Line("G30"), false), // Return to home - alternate G28
                // The following should all be performed as a part of M2 or M30
                //(new Line("G92.2"), false), // Coordinate System Offset: Reset - not including parameters
                //(new Line("G54"), false), // Select Coordinate System: 1 (Default)
                //(new Line("G17"), false), // Plane selection: XY
                //(new Line("G90"), false), // Set Distance Mode: Absolute
                //(new Line("G94"), false), // Set Feed Rate Mode: {lengthUnits} or degrees, per minute
                //(new Line("M48"), false), // Speed and Feed Overrides: Enable
                //(new Line("G40"), false), // Cutter Radius Compensation: Off
                //(new Line("M5"), false), // Turn Spindle: Stop
                //(new Line("G1"), false), // Linear motion: at Feed Rate
                //(new Line("M9"), false), // Coolant: Mist and Flood, Off
                (new Line("M2"), false), // Program stop, alternates M0, M1, M30, M60
            ]
        );

        return context;
    }

    public const string PreambleCompletion = "(Preamble completion by GCodeClean)";
    public const string PreambleCompleted = "(Preamble completed by GCodeClean)";
    public const string PostAmbleCompleted = "(Postamble completed by GCodeClean)";
}
