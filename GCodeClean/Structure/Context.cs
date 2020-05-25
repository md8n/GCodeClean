// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Structure
{
    /// <summary>
    /// Defines a 'context' of Lines
    /// </summary>
    public class Context
    {
        // Non-modal group 0
        // These describe actions that have no effect (G4), or that affect offsets only
        // Officially this includes G28, G30 and G53 - but these are actually special motion commands
        public readonly List<Token> ModalNon = new List<Token> {
            new Token("G4"),
            new Token("G10"),
            new Token("G92"), new Token("G92.1"), new Token("G92.2"), new Token("G92.3")
        };

        // G Modal group 1 - motion
        public readonly List<Token> ModalMotion = new List<Token> {
            new Token("G0"), new Token("G1"), new Token("G2"), new Token(" G3"),
            new Token("G28"), new Token("G30"), new Token("G53"), // For our purposes these are motion commands
            new Token("G38.2"),
            new Token("G80"), new Token("G81"), new Token("G82"), new Token("G83"), new Token("G84"),
            new Token("G85"), new Token("G86"), new Token("G87"), new Token("G88"), new Token("G89")
        };

        // G Modal group 2 - plane selection
        public readonly List<Token> ModalPlane = new List<Token> {
            new Token("G17"), new Token("G18"), new Token("G19")
        };

        // G Modal group 3 - distance mode
        public readonly List<Token> ModalDistance = new List<Token> {
            new Token("G90"), new Token("G91")
        };

        // G Modal group 5 - feed rate mode
        public readonly List<Token> ModalFeedRate = new List<Token> {
            new Token("G93"), new Token("G94")
        };

        // G Modal group 6 - units
        public readonly List<Token> ModalUnits = new List<Token> {
            new Token("G20"), new Token("G21")
        };
        
        // G Modal group 7 - cutter radius compensation
        public readonly List<Token> ModalCutterRadiusCompensation = new List<Token> {
            new Token("G40"), new Token("G41"), new Token("G42")
        };

        // G Modal group 8 - tool length offset
        public readonly List<Token> ModalToolLengthOffset = new List<Token> {
            new Token("G43"), new Token("G49")
        };
        
        // G Modal group 10 - return mode in canned cycles
        public readonly List<Token> ModalReturnMode = new List<Token> {
            new Token("G98"), new Token("G99")
        };

        // G Modal group 12 - coordinate system selection
        public readonly List<Token> ModalCoordinateSystem = new List<Token> {
            new Token("G54"), new Token("G55"), new Token("G56"),
            new Token("G57"), new Token("G58"), new Token("G59"),
            new Token("G59.1"), new Token("G59.2"), new Token("G59.3")
        };

        // Modal group 13 - path control mode
        public readonly List<Token> ModalPathControl = new List<Token> {
            new Token("G61"), new Token("G61.1"), new Token("G64")
        };
        
        // M Modal group 4 - stopping
        public readonly List<Token> ModalStopping = new List<Token> {
            new Token("M0"), new Token("M1"), new Token("M60"), // Temporary (i.e. pause)
            new Token("M2"), new Token("M30") // Actually Stopping
        };

        // M Modal group - tool change
        public readonly List<Token> ModalToolChange = new List<Token> {
            new Token("M6")
        };

        // M Modal group 7 - spindle turning
        public readonly List<Token> ModalSpindleTurning = new List<Token> {
            new Token("M3"), new Token("M4"), new Token("M5")
        };

        // M Modal group 8 - coolant (special case: M7 and M8 may be active at the same time)
        public readonly List<Token> ModalCoolant = new List<Token> {
            new Token("M7"), new Token("M8"), new Token("M9")
        };

        // M Modal group 9 - enable/disable feed and speed override switches
        public readonly List<Token> ModalOverrideEnabling = new List<Token> {
            new Token("M48"), new Token("M49")
        };

        private List<(Line line, bool isOutput)> _lines;

        public List<(Line line, bool isOutput)> Lines
        {
            get => _lines ??= new List<(Line line, bool isOutput)>();
            set
            {
                if (value == null || value.Count == 0)
                {
                    _lines = new List<(Line line, bool isOutput)>();
                    return;
                }

                if (_lines == null)
                {
                    _lines = new List<(Line line, bool isOutput)>();
                }

                foreach (var (line, isOutput) in value)
                {
                    Update(line, isOutput);
                }

                // Stopping or Motion alter the Context but are not formally a part of it
            }
        }

        public Context(List<(Line line, bool isOutput)> lines)
        {
            Lines = lines;
        }

        public void Update(Line line, bool isOutput = false)
        {
            UpdateModal(line, isOutput, ModalFeedRate);
            UpdateModal(line, isOutput, 'F');
            UpdateModal(line, isOutput, 'S');
            UpdateModal(line, isOutput, 'T');
            UpdateModal(line, isOutput, ModalToolChange);
            UpdateModal(line, isOutput, ModalSpindleTurning);
            // No support for Coolants in the context yet
            UpdateModal(line, isOutput, ModalOverrideEnabling);
            // Dwell (G4) - we don't care about here
            UpdateModal(line, isOutput, ModalPlane);
            UpdateModal(line, isOutput, ModalUnits);
            UpdateModal(line, isOutput, ModalCutterRadiusCompensation);
            UpdateModal(line, isOutput, ModalToolLengthOffset);
            UpdateModal(line, isOutput, ModalCoordinateSystem);
            UpdateModal(line, isOutput, ModalPathControl);
            UpdateModal(line, isOutput, ModalDistance);
            UpdateModal(line, isOutput, ModalReturnMode);
            UpdateModal(line, isOutput, ModalNon);
            UpdateModal(line, isOutput, ModalToolChange);
            UpdateModal(line, isOutput, ModalCoolant);
        }

        /// <summary>
        /// Output all lines flagged as not yet output (isOutput == false)
        /// </summary>
        /// <remarks>This does NOT change the isOutput flag to true</remarks>
        /// <returns></returns>
        public List<Line> NonOutputLines()
        {
            return Lines.Where(l => !l.isOutput).Select(l => l.line).ToList();
        }

        /// <summary>
        /// Flag all lines as output (isOutput == true)
        /// </summary>
        public void FlagAllLinesAsOutput()
        {
            for (var ix = 0; ix < Lines.Count; ix++)
            {
                var line = _lines[ix];
                line.isOutput = true;
                _lines[ix] = line;
            }
        }

        private void UpdateModal(Line line, bool isOutput, IReadOnlyCollection<Token> modal)
        {
            var lineTokens = line.Tokens.Intersect(modal).LastOrDefault();
            if (lineTokens == null)
            {
                return;
            }

            var hasReplaced = false;
            for (var ix = Lines.Count - 1; ix >= 0; ix--)
            {
                var inContext = _lines[ix].line.Tokens.Intersect(modal).ToList();
                if (inContext.Count > 0)
                {
                    if (!hasReplaced)
                    {
                        _lines[ix] = (line, isOutput);
                        hasReplaced = true;
                    }
                    else
                    {
                        _lines.RemoveAt(ix);
                    }
                }
            }
            if (!hasReplaced)
            {
                _lines.Add((line, isOutput));
            }
        }

        private void UpdateModal(Line line, bool isOutput, char code)
        {
            var lineTokens = line.Tokens.LastOrDefault(t => t.Code == code);
            if (lineTokens == null)
            {
                return;
            }

            var hasReplaced = false;
            for (var ix = Lines.Count - 1; ix >= 0; ix--)
            {
                var inContext = _lines[ix].line.Tokens.Where(t => t.Code == code).ToList();
                if (inContext.Count > 0)
                {
                    if (!hasReplaced)
                    {
                        _lines[ix] = (line, isOutput);
                        hasReplaced = true;
                    }
                    else
                    {
                        _lines.RemoveAt(ix);
                    }
                }
            }
            if (!hasReplaced)
            {
                _lines.Add((line, isOutput));
            }
        }

        //public void UpdateCoolantModal(Line line, bool isOutput)
        //{
        //    var lineTokens = line.Tokens.Intersect(ModalCoolant).ToList();
        //    if (lineTokens.Count == 0)
        //    {
        //        return;
        //    }

        //    // This is a bitflag: 0 = not set, 1 = M7, 2 = M8, 4 = M9
        //    // It is determined by going backwards through the line
        //    var coolantState = 0;
        //    for (var ix = lineTokens.Count - 1; ix >= 0; ix--)
        //    {
        //        if (coolantState >= 4)
        //        {
        //            break;
        //        }

        //        var hasM7 = lineTokens[ix].ToString() == "M7";
        //        var hasM8 = lineTokens[ix].ToString() == "M8";
        //        var hasM9 = lineTokens[ix].ToString() == "M9";
        //        if (((coolantState | 1) == 1 || !hasM7) && ((coolantState | 2) == 2 || !hasM8) && !hasM9)
        //        {
        //            continue;
        //        }

        //        if (hasM7)
        //        {
        //            coolantState |= 1;
        //        }
        //        else if (hasM8)
        //        {
        //            coolantState |= 2;
        //        }
        //        else
        //        {
        //            coolantState |= 4;
        //        }
        //    }

        //    var hasReplaced = false;
        //    for (var ix = Lines.Count - 1; ix >= 0; ix--)
        //    {
        //        var inContext = new Line(_lines[ix].line.Tokens.Intersect(ModalCoolant));

        //        var hasM7 = inContext.HasToken("M7");
        //        var hasM8 = inContext.HasToken("M8");
        //        var hasM9 = inContext.HasToken("M9");

        //        if (coolantState > 2)
        //        {
        //            if (inContext.Count > 0 && !hasReplaced)
        //            {
        //                _lines[ix] = (line, isOutput);
        //                hasReplaced = true;
        //            }
        //            else
        //            {
        //                _lines.RemoveAt(ix);
        //            }
        //        }
        //        else if (inContext.Count > 0)
        //        {

        //        }

        //    }
        //    if (!hasReplaced)
        //    {
        //        _lines.Add((line, isOutput));
        //    }
        //}
    }
}
