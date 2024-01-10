// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System.Collections.Immutable;

namespace GCodeClean.Structure
{
    /// <summary>
    /// Defines the 'modal groups'.
    /// </summary>
    /// <remarks>There is some variation from the official specification here. Because the official spec ...</remarks>
    public static class ModalGroup {
        /// <summary>
        /// Non-modal subgroup 0 - dwell - G4
        /// </summary>
        public static readonly ImmutableList<Token> ModalDwell = [new Token("G4")];
        /// <summary>
        /// Non-modal subgroup 0 - home - G28, G30
        /// </summary>
        /// <remarks>These are actually special motion commands</remarks>
        public static readonly ImmutableList<Token> ModalHome = [new Token("G28"), new Token("G30")];

        /// <summary>
        /// Non-modal subgroup 0 - change coordinate system data - G10
        /// </summary>
        public static readonly ImmutableList<Token> ModalChangeCoordinateSystemData = [new Token("G10")];

        /// <summary>
        /// Non-modal subgroup 0 - coordinate system offset - G92, G92.1, G92.2, G92.3
        /// </summary>
        public static readonly ImmutableList<Token> ModalCoordinateSystemOffset = [new Token("G92"), new Token("G92.1"), new Token("G92.2"), new Token("G92.3")];

        /// <summary>
        /// Non-modal group 0 - These describe actions that have no effect past the line they are on (G4, (P)),
        /// or that affect offsets only (G10, G92, G92.1, G92.2, G92.3) 
        /// </summary>
        /// <remarks>
        /// Officially this includes G28, G30 and G53 - but these are actually special motion commands
        /// </remarks>
        public static readonly ImmutableList<Token> ModalNon = [..ModalDwell, ..ModalChangeCoordinateSystemData, ..ModalCoordinateSystemOffset];

        /// <summary>
        /// G Modal subgroup 1 - motion - G0, G1, G2, G3
        /// </summary>
        public static readonly ImmutableList<Token> ModalSimpleMotion = [new Token("G0"), new Token("G1"), new Token("G2"), new Token(" G3")];

        /// <summary>
        /// G Modal subgroup 1 - motion - G38.2
        /// </summary>
        public static readonly ImmutableList<Token> ModalProbeMotion = [new Token("G38.2")];

        /// <summary>
        /// G Modal subgroup 1 - canned motion - G80, G81, G82, G83, G84, G85, G86, G87, G88, G89
        /// </summary>
        public static readonly ImmutableList<Token> ModalCannedMotion =
        [
            new Token("G80"), new Token("G81"), new Token("G82"), new Token("G83"), new Token("G84"),
            new Token("G85"), new Token("G86"), new Token("G87"), new Token("G88"), new Token("G89"),
        ];

        /// <summary>
        /// G Modal group 1 - motion - G0, G1, G2, G3, G38.2, G80, G81, G82, G83, G84, G85, G86, G87, G88, G89
        /// </summary>
        public static readonly ImmutableList<Token> ModalMotion = [..ModalSimpleMotion, ..ModalProbeMotion, ..ModalCannedMotion];

        /// <summary>
        /// G Modal group 1 - collective - simple motion, canned motion, home motion, special motion, G53
        /// </summary>
        public static readonly ImmutableList<Token> ModalAllMotion = [..ModalMotion,
            ..ModalHome, // For our purposes these and G53 are motion commands
            new Token("G53")];

        // G Modal group 2 - plane selection
        public static readonly ImmutableList<Token> ModalPlane = [new Token("G17"), new Token("G18"), new Token("G19")];

        // G Modal group 3 - distance mode
        public static readonly ImmutableList<Token> ModalDistance = [new Token("G90"), new Token("G91")];

        /// <summary>
        /// G Modal group 5 - feed rate mode - G93, G94
        /// </summary>
        public static readonly ImmutableList<Token> ModalFeedRate = [new Token("G93"), new Token("G94")];

        /// <summary>
        /// G Modal group 6 - units - G20, G21
        /// </summary>
        public static readonly ImmutableList<Token> ModalUnits = [new Token("G20"), new Token("G21")];

        /// <summary>
        /// G Modal group 7 - cutter radius compensation - G40, G41, G42
        /// </summary>
        public static readonly ImmutableList<Token> ModalCutterRadiusCompensation = [new Token("G40"), new Token("G41"), new Token("G42")];

        /// <summary>
        /// G Modal group 8 - tool length offset - G43, G49
        /// </summary>
        public static readonly ImmutableList<Token> ModalToolLengthOffset = [new Token("G43"), new Token("G49")];

        /// <summary>
        /// G Modal group 10 - return mode in canned cycles - G98, G99
        /// </summary>
        public static readonly ImmutableList<Token> ModalReturnMode = [new Token("G98"), new Token("G99")];

        /// <summary>
        /// G Modal group 12 - coordinate system selection - G54, G55, G56, G57, G58, G59, G59.1, G59.2, G59.3
        /// </summary>
        public static readonly ImmutableList<Token> ModalCoordinateSystem =
        [
            new Token("G54"), new Token("G55"), new Token("G56"),
            new Token("G57"), new Token("G58"), new Token("G59"),
            new Token("G59.1"), new Token("G59.2"), new Token("G59.3"),
        ];

        /// <summary>
        /// G Modal group 13 - path control mode - G61, G61.1, G64
        /// </summary>
        public static readonly ImmutableList<Token> ModalPathControl = [new Token("G61"), new Token("G61.1"), new Token("G64")];

        /// <summary>
        /// M Modal subgroup 4 - stopping - M0, M1, M60
        /// </summary>
        /// <remarks>temporary stops</remarks>
        public static readonly ImmutableList<Token> ModalPausing =
        [
            new Token("M0"), new Token("M1"), new Token("M60"), // Temporary stops
        ];
        /// <summary>
        /// M Modal subgroup 4 - stopping - M2, M30
        /// </summary>
        /// <remarks>actually stopping</remarks>
        public static readonly ImmutableList<Token> ModalStopping =
        [
            new Token("M2"), new Token("M30"), // Actually Stopping
        ];
        /// <summary>
        /// M Modal group 4 - stopping - M0, M2, M2, M30, M60
        /// </summary>
        public static readonly ImmutableList<Token> ModalAllStop = [..ModalPausing, ..ModalStopping];

        /// <summary>
        /// M Modal group - tool change - M6
        /// </summary>
        public static readonly ImmutableList<Token> ModalToolChange = [new Token("M6")];

        /// <summary>
        /// M Modal group 7 - spindle turning - M3, M4, M5
        /// </summary>
        public static readonly ImmutableList<Token> ModalSpindleTurning = [new Token("M3"), new Token("M4"), new Token("M5")];

        /// <summary>
        /// M Modal group 8 - coolant - M7, M8, M9
        /// </summary>
        /// <remarks>special case: M7 and M8 may be active at the same time</remarks>
        public static readonly ImmutableList<Token> ModalCoolant = [new Token("M7"), new Token("M8"), new Token("M9")];

        /// <summary>
        /// M Modal group 9 - enable/disable feed and speed override switches - M48, M49
        /// </summary>
        public static readonly ImmutableList<Token> ModalOverrideEnabling = [new Token("M48"), new Token("M49")];
    }
}
