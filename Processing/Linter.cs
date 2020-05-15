// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Processing
{
    public static class Linter
    {
        public static async IAsyncEnumerable<List<string>> SingleCommandPerLine(this IAsyncEnumerable<List<string>> tokenizedLines)
        {
            await foreach (var tokens in tokenizedLines)
            {
                if (tokens.IsNotCommandCodeOrArguments() 
                    || (tokens.Count(t => Utility.Commands.Contains(t[0])) + tokens.Count(t => Utility.Codes.Contains(t[0]))) <= 1)
                {
                    yield return tokens;
                    continue;
                }

                var tokenList = tokens;
                var yieldableTokens = new List<List<string>>();
                var yieldingTokens = new List<List<string>>();

                // Keep splitting out tokens according to execution order until the line has been 'appropriately' simplified

                // 1. comment (includes message). - These we won't split out for now, but ...

                // 1A. line numbers - we'll prepend these to the first set of yieldableTokens we return
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCode('N');
                var lineNumberToken = yieldableTokens.Count > 0 ? yieldableTokens[0][0] : "";

                // 2. set feed rate mode (G93, G94 — inverse time or per minute).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G93", "G94" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 3. set feed rate (F).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCode('F');
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 4. set spindle speed (S).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCode('S');
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 5. select tool (T).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCode('T');
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 6. change tool (M6).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "M6", "M06" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 7. spindle on or off (M3, M4, M5).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "M3", "M4", "M5", "M03", "M04", "M05" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 8. coolant on or off (M7, M8, M9).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "M7", "M8", "M9", "M07", "M08", "M09" }, true);
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 9. enable or disable overrides (M48, M49).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "M48", "M49" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 10. dwell (G4).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G4", "G04" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 11. set active plane (G17, G18, G19).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G17", "G18", "G19" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 12. set length units (G20, G21).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G20", "G21" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 13. cutter radius compensation on or off (G40, G41, G42)
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G40", "G41", "G42" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 14. cutter length compensation on or off (G43, G49)
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G43", "G49" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 15. coordinate system selection (G54, G55, G56, G57, G58, G59, G59.1, G59.2, G59.3).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G54", "G55", "G56", "G57", "G58", "G59", "G59.1", "G59.2", "G59.3" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 16. set path control mode (G61, G61.1, G64)
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G61", "G61.1", "G64" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 17. set distance mode (G90, G91).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G90", "G91" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 18. set retract mode (G98, G99).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G98", "G99" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 19. home (G28, G30) or change coordinate system data (G10) or set axis offsets (G92, G92.1, G92.2, G94).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G28", "G30" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G10" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G92", "G92.1", "G92.2", "G94" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // 20. perform motion (G0 to G3, G80 to G89), as modified (possibly) by G53.
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G53" });
                (yieldingTokens, lineNumberToken) = yieldingTokens.BuildYieldingTokens(yieldableTokens, lineNumberToken);

                // But now we extract the 'last' tokens first
                // 21. stop (M0, M1, M2, M30, M60).
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "M0", "M1", "M2", "M00", "M01", "M02", "M30", "M60" });
                var stopTokens = yieldableTokens.Count > 0 ? yieldableTokens[0] : new List<string>();

                // And then get the motion tokens
                (tokenList, yieldableTokens) = tokenList.SplitOutSelectedCommands(new List<string> { "G0", "G1", "G2", "G3", "G00", "G01", "G02", "G03", "G80", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89" });
                if (yieldableTokens.Count > 0)
                {
                    tokenList = yieldableTokens[0].Concat(tokenList).ToList();
                }

                foreach (var yieldingToken in yieldingTokens)
                {
                    yield return yieldingToken;
                }
                yield return tokenList;
                yield return stopTokens;
            }
        }

        private static (List<List<string>> yieldingTokens, string lineNumberToken) BuildYieldingTokens(
            this List<List<string>> yieldingTokens, List<List<string>> yieldableTokens, string lineNumberToken)
        {
            if (yieldableTokens.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(lineNumberToken)) {
                    yieldableTokens[0].Prepend(lineNumberToken);
                    lineNumberToken = "";
                }
                yieldingTokens = yieldingTokens.Concat(yieldableTokens).ToList();
            }

            return (yieldingTokens, lineNumberToken);
        }

        private static (List<string> tokens, List<List<string>> yieldableTokens) SplitOutSelectedCode(this List<string> tokens, char code)
        {
            var yieldableTokens = new List<List<string>>();
            var selectedTokens = tokens.Where(t => t[0] == code).ToList();
            if (selectedTokens.Count > 0)
            {
                yieldableTokens.Add(new List<string> { selectedTokens.Last() });
                tokens = tokens.Where(t => t[0] != code).ToList();
            }

            return (tokens, yieldableTokens);
        }

        private static (List<string> tokens, List<List<string>> yieldableTokens) SplitOutSelectedCommands(this List<string> tokens, List<string> commands, bool takeLast = true)
        {
            var yieldableTokens = new List<List<string>>();
            var selectedTokens = tokens.Where(t => commands.Contains(t)).ToList();
            if (selectedTokens.Count > 0)
            {
                foreach (var selectedToken in selectedTokens)
                {
                    yieldableTokens.Add(new List<string> { selectedToken });
                }
                tokens = tokens.Where(t => !commands.Contains(t)).ToList();
            }

            return (tokens, yieldableTokens);
        }
    }
}
