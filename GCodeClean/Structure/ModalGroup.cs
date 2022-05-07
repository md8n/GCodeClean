// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Immutable;
using System.Linq;

namespace GCodeClean.Structure
{
    /// <summary>
    /// Defines the 'modal groups'.
    /// </summary>
    /// <remarks>There is some variation from the official specification here. Because the official spec ...</remarks>
    public static class ModalGroup {
        public static readonly ImmutableList<Token> ModalDwell = ImmutableList.Create(new Token("G4"));

        public static readonly ImmutableList<Token> ModalHome = ImmutableList.Create(new Token("G28"), new Token("G30"));

        // change coordinate system data
        public static readonly ImmutableList<Token> ModalChangeCoordinateSystemData = ImmutableList.Create(new Token("G10"));

        // coordinate system offset
        public static readonly ImmutableList<Token> ModalCoordinateSystemOffset = ImmutableList.Create(
            new Token("G92"), new Token("G92.1"), new Token("G92.2"), new Token("G92.3"));

        // Non-modal group 0
        // These describe actions that have no effect (G4), or that affect offsets only
        // Officially this includes G28, G30 and G53 - but these are actually special motion commands
        public static readonly ImmutableList<Token> ModalNon = ModalDwell
            .AddRange(ModalChangeCoordinateSystemData)
            .AddRange(ModalCoordinateSystemOffset);

        // G Modal group 1 - motion
        public static readonly ImmutableList<Token> ModalSimpleMotion = ImmutableList.Create(
                new Token("G0"), new Token("G1"), new Token("G2"), new Token(" G3"),
                new Token("G38.2"));

        public static readonly ImmutableList<Token> ModalCannedMotion = ImmutableList.Create(
                new Token("G80"), new Token("G81"), new Token("G82"), new Token("G83"), new Token("G84"),
                new Token("G85"), new Token("G86"), new Token("G87"), new Token("G88"), new Token("G89"));

        public static readonly ImmutableList<Token> ModalMotion = ModalSimpleMotion.AddRange(ModalCannedMotion);

        public static readonly ImmutableList<Token> ModalAllMotion = ModalMotion
            .AddRange(ModalHome) // For our purposes these and G53 are motion commands
            .AddRange(ImmutableList.Create(new Token("G53")));

        // G Modal group 2 - plane selection
        public static readonly ImmutableList<Token> ModalPlane = ImmutableList.Create(
            new Token("G17"), new Token("G18"), new Token("G19"));

        // G Modal group 3 - distance mode
        public static readonly ImmutableList<Token> ModalDistance = ImmutableList.Create(
            new Token("G90"), new Token("G91"));

        // G Modal group 5 - feed rate mode
        public static readonly ImmutableList<Token> ModalFeedRate = ImmutableList.Create(
            new Token("G93"), new Token("G94"));

        // G Modal group 6 - units
        public static readonly ImmutableList<Token> ModalUnits = ImmutableList.Create(
            new Token("G20"), new Token("G21"));

        // G Modal group 7 - cutter radius compensation
        public static readonly ImmutableList<Token> ModalCutterRadiusCompensation = ImmutableList.Create(
            new Token("G40"), new Token("G41"), new Token("G42"));

        // G Modal group 8 - tool length offset
        public static readonly ImmutableList<Token> ModalToolLengthOffset = ImmutableList.Create(
            new Token("G43"), new Token("G49"));

        // G Modal group 10 - return mode in canned cycles
        public static readonly ImmutableList<Token> ModalReturnMode = ImmutableList.Create(
            new Token("G98"), new Token("G99"));

        // G Modal group 12 - coordinate system selection
        public static readonly ImmutableList<Token> ModalCoordinateSystem = ImmutableList.Create(
            new Token("G54"), new Token("G55"), new Token("G56"),
            new Token("G57"), new Token("G58"), new Token("G59"),
            new Token("G59.1"), new Token("G59.2"), new Token("G59.3"));

        // Modal group 13 - path control mode
        public static readonly ImmutableList<Token> ModalPathControl = ImmutableList.Create(
            new Token("G61"), new Token("G61.1"), new Token("G64"));

        // M Modal group 4 - stopping
        public static readonly ImmutableList<Token> ModalPausing = ImmutableList.Create(
            new Token("M0"), new Token("M1"), new Token("M60") // Temporary stops
        );
        public static readonly ImmutableList<Token> ModalStopping = ImmutableList.Create(
            new Token("M2"), new Token("M30") // Actually Stopping
        );
        public static readonly ImmutableList<Token> ModalAllStop = ModalPausing.AddRange(ModalStopping);

        // M Modal group - tool change
        public static readonly ImmutableList<Token> ModalToolChange = ImmutableList.Create(
            new Token("M6"));

        // M Modal group 7 - spindle turning
        public static readonly ImmutableList<Token> ModalSpindleTurning = ImmutableList.Create(
            new Token("M3"), new Token("M4"), new Token("M5"));

        // M Modal group 8 - coolant (special case: M7 and M8 may be active at the same time)
        public static readonly ImmutableList<Token> ModalCoolant = ImmutableList.Create(
            new Token("M7"), new Token("M8"), new Token("M9"));

        // M Modal group 9 - enable/disable feed and speed override switches
        public static readonly ImmutableList<Token> ModalOverrideEnabling = ImmutableList.Create(
            new Token("M48"), new Token("M49"));
    }
}
