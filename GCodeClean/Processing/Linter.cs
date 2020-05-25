// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Linter
    {
        public static async IAsyncEnumerable<Line> SingleCommandPerLine(this IAsyncEnumerable<Line> tokenisedLines)
        {
            await foreach (var line in tokenisedLines)
            {
                if (line.IsNotCommandCodeOrArguments() 
                    || line.Tokens.Count(t => t.IsCommand) + line.Tokens.Count(t => t.IsCode) <= 1)
                {
                    yield return line;
                    continue;
                }

                var currentLine = line;
                List<Line> yieldableLines;
                var yieldingLines = new List<Line>();

                // Keep splitting out tokens into individual lines according to execution order until the line has been 'appropriately' simplified

                // 1. comment (includes message). - These we won't split out for now, but ...

                // 1A. line numbers - we'll prepend these to the first set of yieldableTokens we return
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCode('N');
                var lineNumberToken = yieldableLines.Count > 0 ? yieldableLines.First().Tokens.First() : new Token("");

                // 2. set feed rate mode (G93, G94 — inverse time or per minute).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalFeedRate);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 3. set feed rate (F).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCode('F');
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 4. set spindle speed (S).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCode('S');
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 5. select tool (T).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCode('T');
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 6. change tool (M6).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalToolChange);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 7. spindle on or off (M3, M4, M5).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalSpindleTurning);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 8. coolant on or off (M7, M8, M9).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalCoolant);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 9. enable or disable overrides (M48, M49).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalOverrideEnabling);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 10. dwell (G4).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalDwell);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 11. set active plane (G17, G18, G19).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalPlane);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 12. set length units (G20, G21).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalUnits);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 13. cutter radius compensation on or off (G40, G41, G42)
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalCutterRadiusCompensation);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 14. cutter length compensation on or off (G43, G49)
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalToolLengthOffset);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 15. coordinate system selection (G54, G55, G56, G57, G58, G59, G59.1, G59.2, G59.3).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalCoordinateSystem);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 16. set path control mode (G61, G61.1, G64)
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalPathControl);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 17. set distance mode (G90, G91).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalDistance);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 18. set retract mode (G98, G99).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalReturnMode);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // 19. home (G28, G30) or change coordinate system data (G10) or set axis offsets (G92, G92.1, G92.2, G94).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalHome);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalChangeCoordinateSystemData);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalCoordinateSystemOffset);
                (yieldingLines, lineNumberToken) = yieldingLines.BuildYieldingLines(yieldableLines, lineNumberToken);

                // But now we extract the 'last' tokens first
                // 21. stop (M0, M1, M2, M30, M60).
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalStopping);
                var stopTokens = yieldableLines.Count > 0 ? yieldableLines[0] : new Line();

                // 20. perform motion (G0 to G3, G38.2, G80 to G89), as modified (possibly) by G53.
                (currentLine, yieldableLines) = currentLine.SplitOutSelectedCommands(ModalGroup.ModalMotion);
                if (yieldableLines.Count > 0)
                {
                    // Note that any G53 will be appended here
                    currentLine.Tokens = yieldableLines[0].Tokens.Concat(currentLine.Tokens).ToList();
                }

                // Specifically not handled are the 'Home' commands G28 and G30

                foreach (var yieldingToken in yieldingLines)
                {
                    if (yieldingToken.IsNotCommandCodeOrArguments())
                    {
                        continue;
                    }
                    yield return yieldingToken;
                }
                yield return currentLine;
                if (!stopTokens.IsNotCommandCodeOrArguments())
                {
                    yield return stopTokens;
                }
            }
        }

        private static (List<Line> yieldingLines, Token lineNumberToken) BuildYieldingLines(
            this List<Line> yieldingLines, IReadOnlyList<Line> yieldableLines, Token lineNumberToken)
        {
            if (yieldableLines.Count > 0)
            {
                if (lineNumberToken.IsValid) {
                    yieldableLines[0].Tokens.Insert(0, lineNumberToken);
                    lineNumberToken.Source = "";
                }
                yieldingLines = yieldingLines.Concat(yieldableLines).ToList();
            }

            return (yieldingLines, lineNumberToken);
        }

        private static (Line line, List<Line> yieldableLines) SplitOutSelectedCode(this Line line, char code)
        {
            var yieldableLines = new List<Line>();
            var selectedTokens = line.Tokens.Where(t => t.Code == code).ToList();
            if (selectedTokens.Count <= 0)
            {
                return (line, yieldableLines);
            }

            yieldableLines.Add(new Line ( selectedTokens.Last().ToString() ));
            line.Tokens = line.Tokens.Where(t => t.Code != code).ToList();

            return (line, yieldableLines);
        }

        private static (Line line, List<Line> yieldableLines) SplitOutSelectedCommands(this Line line, IEnumerable<string> commands)
        {
            var tokens = commands.Select(c => new Token(c)).ToList();

            return line.SplitOutSelectedCommands(tokens);
        }

        private static (Line line, List<Line> yieldableLines) SplitOutSelectedCommands(this Line line, ICollection<Token> commands)
        {
            var yieldableLines = new List<Line>();
            var selectedTokens = line.Tokens.Intersect(commands).ToList();
            if (selectedTokens.Count <= 0)
            {
                return (line, yieldableLines);
            }

            foreach (var selectedToken in selectedTokens)
            {
                yieldableLines.Add(new Line (selectedToken));
            }
            line.Tokens = line.Tokens.Except(commands).ToList();

            return (line, yieldableLines);
        }
    }
}
