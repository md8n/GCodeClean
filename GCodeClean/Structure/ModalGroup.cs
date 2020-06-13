// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Structure
{
    /// <summary>
    /// Defines the 'modal groups'.
    /// </summary>
    /// <remarks>There is some variation from the official specification here. Because the official spec ...</remarks>
    public static class ModalGroup
    {
        public static readonly List<Token> ModalDwell = new List<Token> {
            new Token("G4")
        };

        public static readonly List<Token> ModalHome = new List<Token> {
            new Token("G28"), new Token("G30")
        };

        // change coordinate system data
        public static readonly List<Token> ModalChangeCoordinateSystemData = new List<Token> {
            new Token("G10")
        };

        // coordinate system offset
        public static readonly List<Token> ModalCoordinateSystemOffset = new List<Token> {
            new Token("G92"), new Token("G92.1"), new Token("G92.2"), new Token("G92.3")
        };

        // Non-modal group 0
        // These describe actions that have no effect (G4), or that affect offsets only
        // Officially this includes G28, G30 and G53 - but these are actually special motion commands
        public static readonly List<Token> ModalNon = ModalDwell
            .Concat(ModalChangeCoordinateSystemData)
            .Concat(ModalCoordinateSystemOffset)
            .ToList();

        // G Modal group 1 - motion
        public static readonly List<Token> ModalSimpleMotion = new List<Token> {
                new Token("G0"), new Token("G1"), new Token("G2"), new Token(" G3"),
                new Token("G38.2")
            };

        public static readonly List<Token> ModalCannedMotion = new List<Token> {
                new Token("G80"), new Token("G81"), new Token("G82"), new Token("G83"), new Token("G84"),
                new Token("G85"), new Token("G86"), new Token("G87"), new Token("G88"), new Token("G89")
            };

        public static readonly List<Token> ModalMotion = ModalSimpleMotion.Concat(ModalCannedMotion).ToList();

        public static readonly List<Token> ModalAllMotion = ModalMotion
            .Concat(ModalHome) // For our purposes these and G53 are motion commands
            .Concat(new List<Token> {new Token("G53")}).ToList();

        // G Modal group 2 - plane selection
        public static readonly List<Token> ModalPlane = new List<Token> {
            new Token("G17"), new Token("G18"), new Token("G19")
        };

        // G Modal group 3 - distance mode
        public static readonly List<Token> ModalDistance = new List<Token> {
            new Token("G90"), new Token("G91")
        };

        // G Modal group 5 - feed rate mode
        public static readonly List<Token> ModalFeedRate = new List<Token> {
            new Token("G93"), new Token("G94")
        };

        // G Modal group 6 - units
        public static readonly List<Token> ModalUnits = new List<Token> {
            new Token("G20"), new Token("G21")
        };
        
        // G Modal group 7 - cutter radius compensation
        public static readonly List<Token> ModalCutterRadiusCompensation = new List<Token> {
            new Token("G40"), new Token("G41"), new Token("G42")
        };

        // G Modal group 8 - tool length offset
        public static readonly List<Token> ModalToolLengthOffset = new List<Token> {
            new Token("G43"), new Token("G49")
        };
        
        // G Modal group 10 - return mode in canned cycles
        public static readonly List<Token> ModalReturnMode = new List<Token> {
            new Token("G98"), new Token("G99")
        };

        // G Modal group 12 - coordinate system selection
        public static readonly List<Token> ModalCoordinateSystem = new List<Token> {
            new Token("G54"), new Token("G55"), new Token("G56"),
            new Token("G57"), new Token("G58"), new Token("G59"),
            new Token("G59.1"), new Token("G59.2"), new Token("G59.3")
        };

        // Modal group 13 - path control mode
        public static readonly List<Token> ModalPathControl = new List<Token> {
            new Token("G61"), new Token("G61.1"), new Token("G64")
        };
        
        // M Modal group 4 - stopping
        public static readonly List<Token> ModalStopping = new List<Token> {
            new Token("M0"), new Token("M1"), new Token("M60"), // Temporary (i.e. pause)
            new Token("M2"), new Token("M30") // Actually Stopping
        };

        // M Modal group - tool change
        public static readonly List<Token> ModalToolChange = new List<Token> {
            new Token("M6")
        };

        // M Modal group 7 - spindle turning
        public static readonly List<Token> ModalSpindleTurning = new List<Token> {
            new Token("M3"), new Token("M4"), new Token("M5")
        };

        // M Modal group 8 - coolant (special case: M7 and M8 may be active at the same time)
        public static readonly List<Token> ModalCoolant = new List<Token> {
            new Token("M7"), new Token("M8"), new Token("M9")
        };

        // M Modal group 9 - enable/disable feed and speed override switches
        public static readonly List<Token> ModalOverrideEnabling = new List<Token> {
            new Token("M48"), new Token("M49")
        };
    }
}
