// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Structure;

/// <summary>
/// Defines a 'context' of Lines
/// </summary>
public class Context
{
    public bool AllLinesOutput { get; private set; } = false;

    private List<(Line line, bool isOutput)> _lines;

    public List<(Line line, bool isOutput)> Lines
    {
        get => _lines ??= [];
        set {
            if (value == null || value.Count == 0) {
                _lines = [];
                return;
            }

            _lines ??= [];

            foreach (var (line, isOutput) in value) {
                Update(line, isOutput);
            }

            // Stopping or Motion alter the Context but are not formally a part of it
        }
    }

    public Context(List<(Line line, bool isOutput)> lines) {
        Lines = lines;
    }

    /// <summary>
    /// Update the context in execution order for each modal group
    /// </summary>
    /// <param name="line"></param>
    /// <param name="isOutput"></param>
    public void Update(Line line, bool isOutput = false) {
        UpdateModal(line, isOutput, ModalGroup.ModalFeedRate);
        UpdateModal(line, isOutput, Letter.feedRate);
        UpdateModal(line, isOutput, Letter.spindleSpeed);
        UpdateModal(line, isOutput, Letter.selectTool);
        UpdateModal(line, isOutput, ModalGroup.ModalToolChange);
        UpdateModal(line, isOutput, ModalGroup.ModalSpindleTurning);
        // No support for Coolants in the context yet
        UpdateModal(line, isOutput, ModalGroup.ModalOverrideEnabling);
        // Dwell (G4) - we don't care about here - it's not a modal
        UpdateModal(line, isOutput, ModalGroup.ModalPlane);
        UpdateModal(line, isOutput, ModalGroup.ModalUnits);
        UpdateModal(line, isOutput, ModalGroup.ModalCutterRadiusCompensation);
        UpdateModal(line, isOutput, ModalGroup.ModalToolLengthOffset);
        UpdateModal(line, isOutput, ModalGroup.ModalCoordinateSystem);
        UpdateModal(line, isOutput, ModalGroup.ModalPathControl);
        UpdateModal(line, isOutput, ModalGroup.ModalDistance);
        UpdateModal(line, isOutput, ModalGroup.ModalReturnMode);
        UpdateModal(line, isOutput, ModalGroup.ModalNon);
        UpdateModal(line, isOutput, ModalGroup.ModalCoolant);
    }

    public Token GetModalState(IReadOnlyCollection<Token> modal) {
        foreach (var (line, _) in Lines) {
            var lineTokens = line.Tokens.Intersect(modal);
            if (!lineTokens.Any()) {
                continue;
            }

            return lineTokens.Last();
        }

        return null;
    }

    public Token GetModalState(char code) {
        foreach (var (line, _) in Lines) {
            var lineTokens = line.Tokens.Where(t => t.Code == code);
            if (!lineTokens.Any()) {
                continue;
            }

            return lineTokens.Last();
        }

        return null;
    }

    /// <summary>
    /// Output all lines flagged as not yet output (isOutput == false)
    /// </summary>
    /// <remarks>This does NOT change the isOutput flag to true</remarks>
    /// <returns></returns>
    public List<Line> NonOutputLines() {
        return Lines.Where(l => !l.isOutput).Select(l => l.line).ToList();
    }

    /// <summary>
    /// Flag all lines as output (isOutput == true)
    /// </summary>
    public void FlagAllLinesAsOutput() {
        for (var ix = 0; ix < Lines.Count; ix++) {
            var line = _lines[ix];
            line.isOutput = true;
            _lines[ix] = line;
        }

        AllLinesOutput = true;
    }

    private void UpdateModal(Line line, bool isOutput, IReadOnlyCollection<Token> modal) {
        if (!line.Tokens.Intersect(modal).Any()) {
            return;
        }

        var hasReplaced = false;
        for (var ix = Lines.Count - 1; ix >= 0; ix--) {
            if (!_lines[ix].line.Tokens.Intersect(modal).Any()) {
                continue;
            }

            if (!hasReplaced) {
                _lines[ix] = (line, isOutput);
                hasReplaced = true;
            } else {
                _lines.RemoveAt(ix);
            }
        }
        if (!hasReplaced) {
            _lines.Add((line, isOutput));
        }
    }

    private void UpdateModal(Line line, bool isOutput, char code) {
        if (!line.Tokens.Exists(t => t.Code == code)) {
            return;
        }

        var hasReplaced = false;
        for (var ix = Lines.Count - 1; ix >= 0; ix--) {
            if (!_lines[ix].line.Tokens.Exists(t => t.Code == code)) {
                continue;
            }

            if (!hasReplaced) {
                _lines[ix] = (line, isOutput);
                hasReplaced = true;
            } else {
                _lines.RemoveAt(ix);
            }
        }
        if (!hasReplaced) {
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

    //    // This is a bit flag: 0 = not set, 1 = M7, 2 = M8, 4 = M9
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
