// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using GCodeClean.Processing;
using GCodeClean.Structure;

using Xunit;
using Xunit.Abstractions;

namespace GCodeClean.Tests;

public class Processing(ITestOutputHelper testOutputHelper) {
    private static async IAsyncEnumerable<Line> AsyncLines(IEnumerable<Line> lines)
    {
        foreach (var line in lines)
        {
            await Task.Delay(1);
            yield return line;
        }
    }

    [Fact]
    public async Task TestInjectPreamble()
    {
        List<string> sourceTextLines = ["G17", "G40", "G90", "G21", "T1", "S10000", "M3", "G0 X35.747 Y46.824"];
        var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
        var sourceLines = sourceTextLines.ToAsyncEnumerable();

        var testLines = sourceLineLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        var zClamp = 3M;
        List<Line> expectedLines = [
            new Line("G17"),
            new Line("G40"),
            new Line("G90"),
            new Line("G21"),
            new Line("T1"),
            new Line("S10000"),
            new Line("M3"),
            new Line(Default.PreambleCompletion),
            new Line("G94"),
            new Line("G49"),
            new Line("G54"),
            new Line(Default.PreambleCompleted),
            new Line(""),
            new Line($"G0 Z{zClamp}"),
            new Line("G0 X35.747 Y46.824")
        ];

        // Note: that cleaning up the redundant preamble context above is performed by DedupContext

        var firstPhaseLines = sourceLines.CleanLinesFirstPhase(false);
        var preambleContext = await firstPhaseLines.BuildPreamble();

        var resultLines = await lines.InjectPreamble(preambleContext, zClamp).ToListAsync();
        Assert.False(sourceLineLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestAugment()
    {
        List<Line> sourceLines = [
            new Line("G21"),
            new Line("G90"),
            new Line("G1 Z-0.15"),
            new Line("X26.6059 Z - 0.1539 F60"),
            new Line("X26.6068 Z - 0.1577")
        ];
        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        List<Line> expectedLines = [
            new Line("G21"),
            new Line("G90"),
            new Line("G1 Z-0.15"),
            new Line("G1 F60 X26.6059 Z-0.1539"),
            new Line("G1 X26.6068 Z-0.1577")
        ];
            
        var resultLines = await lines.Augment().ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestZClampMM()
    {
        List<Line> sourceLines = [
            new Line("G21"),
            new Line("G90"),
            new Line("G0 Z1.5"),
            new Line("G0 X14.723 Y97.714"),
            new Line("G0 Z0.5"),
            new Line("G1 Z-1.135"),
            new Line("G0 Z0.5"),
            new Line("G0 Z1.5"),
            new Line("G0 X34.033 Y100.094"),
            new Line("G0 X54.033 Y136.094"),
            new Line("G1 Z-0.249"),
            new Line("G1 X54.125 Y136.167 Z-0.307"),
            new Line("G1 X54.178 Y136.211 Z-0.339"),
        ];

        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        var setHeight = 1.1M;
        List<Line> expectedLines = [
            new Line("G21"),
            new Line("G90"),
            new Line($"G0 Z{setHeight}"),
            new Line("G0 X14.723 Y97.714"),
            new Line($"G0 Z{setHeight}"),
            new Line("G1 Z-1.135"),
            new Line($"G0 Z{setHeight}"),
            new Line($"G0 Z{setHeight}"),
            new Line("G0 X34.033 Y100.094"),
            new Line("G0 X54.033 Y136.094"),
            new Line("G1 Z-0.249"),
            new Line("G1 X54.125 Y136.167 Z-0.307"),
            new Line("G1 X54.178 Y136.211 Z-0.339"),
        ];

        var resultLines = await lines.ZClamp(setHeight).ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestZClampInch()
    {
        List<string> sourceTextLines = ["G20", "G90", "G0 Z0.15", "G0 Z0.05", "G1 Z0.0394 F30", "G1 Z-0.15"];
        var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
        var sourceLines = sourceTextLines.ToAsyncEnumerable();

        var testLines = sourceLineLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        var setHeight = 1.1M;

        var firstPhaseLines = sourceLines.CleanLinesFirstPhase(false);
        var preambleContext = await firstPhaseLines.BuildPreamble();

        var adjustedHeight = Utility.ConstrictZClamp(Utility.GetLengthUnits(preambleContext), setHeight);
        List<Line> expectedLines = [
            new Line("G20"),
            new Line("G90"),
            new Line($"G0 Z{adjustedHeight}"),
            new Line($"G0 Z{adjustedHeight}"),
            new Line($"G0 Z{adjustedHeight} F30"),
            new Line("G1 Z-0.15")
        ];

        var resultLines = await lines.ZClamp(adjustedHeight).ToListAsync();
        Assert.False(sourceLineLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestClip()
    {
        List<Line> sourceLines = [
            new Line("G21"),
            new Line("G90"),
            new Line("G1 Z0.15678 F9.0")
        ];

        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);

        var resultLines = await lines.Clip().ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestAnnotate()
    {
        var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
           ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        var tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";

        JsonDocument tokenDefinitions;
        try
        {
            var tokenDefsSource = File.ReadAllText(tokenDefsPath);
            tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
        }
        catch (FileNotFoundException fileNotFoundEx)
        {
            Console.WriteLine($"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
            return;
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}");
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        List<Line> sourceLines = [
            new Line("G21"),
            new Line("G90"),
            new Line("G1 Z0.15678 F9.0"),
            new Line("/G1234")
        ];
        List<string> expectedLines = [
            "G21 (Length units: Millimeters)",
            "G90 (Set Distance Mode: Absolute)",
            "G1 Z0.1568 F9 (Linear motion: at Feed Rate, Z0.1568mm, Set Feed Rate to 9 {feedRateMode})",
            "/ G1234 (Block Delete)"
        ];

        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);

        var resultLines = await lines.Annotate(tokenDefinitions.RootElement).JoinLines("SOFT").ToListAsync();

        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task TestDetectTravelling() {
        List<string> sourceTextLines = [
            "G0 Z1.5",
            "G0 X68.904 Y128.746 Z1.5",
            "G1 X68.904 Y128.746 Z-1.194",
            "G1 X68.995 Y128.814 Z-1.254",
            "G1 X69.089 Y128.892 Z-1.322",
            "G0 Z1.5",
            "G0 X42.239 Y157.031",
            "G1 Z-0.413",
        ];
        var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
        var sourceLines = sourceTextLines.ToAsyncEnumerable();

        var zClamp = 1.5M;

        List<Line> expectedLines = [
            new Line("G0 Z1.5"),
            new Line("G0 X68.904 Y128.746 Z1.5"),
            new Line("G1 X68.904 Y128.746 Z-1.194"),
            new Line("G1 X68.995 Y128.814 Z-1.254"),
            new Line("G1 X69.089 Y128.892 Z-1.322"),
            new Line("G0 X69.089 Y128.892 Z1.5 (||Travelling||0||0||0||-1.322||notset||>>G0 X68.904 Y128.746 Z1.5>>G0 X69.089 Y128.892 Z1.5>>||)"),
            new Line("G0 X42.239 Y157.031 Z1.5"),
            new Line("G1 X42.239 Y157.031 Z-0.413"),
        ];

        var resultLines = await sourceLines.CleanLinesFirstPhase(false).DetectTravelling(zClamp).ToListAsync();
        Assert.False(sourceLineLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }
}
