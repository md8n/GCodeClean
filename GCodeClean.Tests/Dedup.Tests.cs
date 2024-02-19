// Copyright (c) 2020-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GCodeClean.Processing;
using GCodeClean.Structure;

using Xunit;
using Xunit.Abstractions;

namespace GCodeClean.Tests;

public class Dedup(ITestOutputHelper testOutputHelper) {
    private static async IAsyncEnumerable<Line> AsyncLines(IEnumerable<Line> lines)
    {
        foreach (var line in lines)
        {
            await Task.Delay(1);
            yield return new Line(line);
        }
    }

    [Fact]
    public async Task DedupContext() {
        List<string> sourceTextLines = ["G17", "G40", "G90", "G20", "T1", "S10000", "M3", "G19", "G0 Z3", "G0 X35.747 Y46.824", "G17"];
        var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));

        var testLines = sourceLineLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        List<Line> expectedLines = [
            new Line("G20"),
            new Line("T1"),
            new Line("S10000"),
            new Line("G19"),
            new Line("G0 Z3"),
            new Line("G0 X35.747 Y46.824"),
            new Line("G17")
        ];

        var resultLines = await lines.DedupContext().ToListAsync();
        Assert.False(sourceLineLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task DedupLinear()
    {
        List<Line> sourceLines = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y0 Z-0.15"),
            new Line("G1 X1 Y1 Z-0.15"),
            new Line("G1 X2 Y2 Z-0.15"),
            new Line("G1 X3 Y3 Z-0.15"),
            new Line("G1 X4 Y4 Z-0.15"),
            new Line("G1 X5.5 Y5 Z-0.15"),
            new Line("G1 X6 Y6 Z-0.15"),
            new Line("G1 X7 Y7 Z-0.15"),
            new Line("G1 X8 Y8 Z-0.15"),
            new Line("G1 X9 Y9 Z-0.15"),
            new Line("M5"),
            new Line("M30"),
        ];

        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);
        List<Line> expectedLinesA = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y0 Z-0.15"),
            new Line("G1 X4 Y4 Z-0.15"),
            new Line("G1 X5.5 Y5 Z-0.15"),
            new Line("G1 X6 Y6 Z-0.15"),
            new Line("G1 X9 Y9 Z-0.15"),
            new Line("M5"),
            new Line("M30"),
        ];
        List<Line> expectedLinesB = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y0 Z-0.15"),
            new Line("G1 X9 Y9 Z-0.15"),
            new Line("M5"),
            new Line("M30"),
        ];

        // A Test with fine tolerance
        var resultLinesA = await lines.DedupLinear(0.005M).ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLinesA));
        Assert.True(expectedLinesA.SequenceEqual(resultLinesA));

        // B Test with coarse tolerance
        var resultLinesB = await lines.DedupLinear(0.5M).ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLinesB));
        Assert.True(expectedLinesB.SequenceEqual(resultLinesB));
    }

    [Fact]
    public async Task DedupLinearToArc()
    {
        List<Line> sourceLines = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y25 Z-0.15"),
            new Line("G1 X2.1789 Y24.9049 Z-0.15"),
            new Line("G1 X4.3412 Y24.6202 Z-0.15"),
            new Line("G1 X6.4705 Y24.1481 Z-0.15"),
            new Line("G1 X8.5505 Y23.4923 Z-0.15"),
            new Line("G1 X10.5655 Y22.6577 Z-0.15"),
            new Line("G1 X12.5 Y21.6506 Z-0.15"),
            new Line("G1 X16.0697 Y19.1511 Z-0.15"),
            new Line("G1 X17.6777 Y17.6777 Z-0.15"),
            new Line("G1 X19.1511 Y16.0697 Z-0.15"),
            new Line("G1 X20.4788 Y14.3394 Z-0.15"),
            new Line("G1 X21.6506 Y12.5 Z-0.15"),
            new Line("M5"),
            new Line("M30"),
        ];

        var testLines = sourceLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);

        List<Line> expectedLinesA = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y25 Z-0.15"),
            new Line("G2 X12.5 Y21.6506 Z-0.15 I0.0006 J-24.9975"),
            new Line("G1 X16.0697 Y19.1511 Z-0.15"),
            new Line("G2 X21.6506 Y12.5 Z-0.15 I-16.0687 J-19.1501"),
            new Line("M5"),
            new Line("M30"),
        ];

        List<Line> expectedLinesB = [
            new Line("G17"),
            new Line("G21"),
            new Line("G90"),
            new Line("G1 X0 Y25 Z-0.15"),
            new Line("G2 X21.6506 Y12.5 Z-0.15 I0.0002 J-24.9995"),
            new Line("M5"),
            new Line("M30"),
        ];

        // A Test with fine tolerance
        var resultLinesA = await lines.DedupLinearToArc(0.005M).ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLinesA));
        Assert.True(expectedLinesA.SequenceEqual(resultLinesA));

        // B Test with coarse tolerance
        var resultLinesB = await lines.DedupLinearToArc(0.5M).ToListAsync();
        Assert.False(sourceLines.SequenceEqual(resultLinesB));
        Assert.True(expectedLinesB.SequenceEqual(resultLinesB));
    }

    [Fact]
    public async Task DedupTravelling() {
        List<string> sourceTextLines = [
            "G17",
            "G90",
            "G21",
            "G00 Z1.5",
            "G00 X14.723 Y97.714",
            "G00 Z0.5",
            "G01 Z-1.135",
            "G00 Z0.5",
            "G00 Z1.5",
            "G00 X34.033 Y100.094",
            "G00 X54.033 Y136.094",
            "G01 Z-0.249",
            "G01 X54.125 Y136.167 Z-0.307",
            "G01 X54.178 Y136.211 Z-0.339",
            "G00 Z0.5",
            "M30",
        ];
        var testLines = sourceTextLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);

        List<Line> expectedLines = [
            new Line("G17"),
            new Line("G90"),
            new Line("G21"),
            new Line("G0 Z0.5"),
            new Line("G0 X14.723 Y97.714"),
            new Line("G1 Z-1.135"),
            new Line("G0 Z0.5"),
            new Line("G0 X54.033 Y136.094"),
            new Line("G1 Z-0.249"),
            new Line("G1 X54.125 Y136.167 Z-0.307"),
            new Line("G1 X54.178 Y136.211 Z-0.339"),
            new Line("G0 Z0.5"),
            new Line("M30"),
        ];

        decimal zClamp = 0.5M;
        var zClampedLines = lines.ZClamp(zClamp);

        var resultLines = await zClampedLines.DedupTravelling().ToArrayAsync(); 
        Assert.False(testLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }

    [Fact]
    public async Task DedupTravellingAgain() {
        List<string> sourceTextLines = [
            "G17",
            "G90",
            "G21",
            "G00 Z1.5",
            "G01 X54.178 Y136.211",
            "G01 X54.178 Y136.211 Z-0.678",
            "G01 X54.125 Y136.168 Z-0.613",
            "G01 X54.033 Y136.095 Z-0.499",
            "G00 Z1.5",
            "G00 X69.089 Y128.892",
            "G01 Z-0.661",
            "G01 X68.995 Y128.814 Z-0.627",
            "G01 X68.905 Y128.746 Z-0.597",
            "G01 X68.813 Y128.684 Z-0.57",
            "G00 Z0.5",
            "M30",
        ];
        var testLines = sourceTextLines.ConvertAll(l => new Line(l));
        var lines = AsyncLines(testLines);

        List<Line> expectedLines = [
            new Line("G17"),
            new Line("G90"),
            new Line("G21"),

            new Line("G0 Z0.5"),
            new Line("G0 X54.178 Y136.211 Z0.5"),
            new Line("G1 X54.178 Y136.211 Z-0.678"),
            new Line("G1 X54.125 Y136.168 Z-0.613"),
            new Line("G1 X54.033 Y136.095 Z-0.499"),
            new Line("G0 X54.033 Y136.095 Z0.5"),

            new Line("G0 X69.089 Y128.892 Z0.5"),
            new Line("G1 X69.089 Y128.892 Z-0.661"),
            new Line("G1 X68.995 Y128.814 Z-0.627"),
            new Line("G1 X68.905 Y128.746 Z-0.597"),
            new Line("G1 X68.813 Y128.684 Z-0.57"),
            new Line("G0 X68.813 Y128.684 Z0.5"),

            new Line("M30"),
        ];

        decimal zClamp = 0.5M;
        var augmentLines = lines.Augment();
        var zClampedLines = augmentLines.ZClamp(zClamp);

        var resultLines = await zClampedLines.DedupTravelling().ToArrayAsync();
        Assert.False(testLines.SequenceEqual(resultLines));
        Assert.True(expectedLines.SequenceEqual(resultLines));
    }
}
