// Copyright (c) 2020-2022 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GCodeClean.Processing;
using GCodeClean.Structure;

using Xunit;
using Xunit.Abstractions;

namespace GCodeClean.Tests
{
    public class Dedup
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public Dedup(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private static async IAsyncEnumerable<Line> AsyncLines(IEnumerable<Line> lines)
        {
            foreach (var line in lines)
            {
                await Task.Delay(1);
                yield return line;
            }
        }

        [Fact]
        public async Task DedupContext() {
            var sourceTextLines = new List<string> { "G17", "G40", "G90", "G20", "T1", "S10000", "M3", "G19", "G0 Z3", "G0 X35.747 Y46.824", "G17" };
            var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));

            var testLines = sourceLineLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var expectedLines = new List<Line> {
                new Line("G20"),
                new Line("T1"),
                new Line("S10000"),
                new Line("G19"),
                new Line("G0 Z3"),
                new Line("G0 X35.747 Y46.824"),
                new Line("G17")
            };

            var resultLines = await lines.DedupContext().ToListAsync();
            Assert.False(sourceLineLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async Task DedupLinear()
        {
            var sourceLines = new List<Line> {
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
            };

            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var expectedLinesA = new List<Line> {
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
            };
            var expectedLinesB = new List<Line> {
                new Line("G17"),
                new Line("G21"),
                new Line("G90"),
                new Line("G1 X0 Y0 Z-0.15"),
                new Line("G1 X9 Y9 Z-0.15"),
                new Line("M5"),
                new Line("M30"),
            };

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
            var sourceLines = new List<Line> {
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
            };

            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);

            var expectedLinesA = new List<Line> {
                new Line("G17"),
                new Line("G21"),
                new Line("G90"),
                new Line("G1 X0 Y25 Z-0.15"),
                new Line("G2 X12.5 Y21.6506 Z-0.15 I0.0006 J-24.9975"),
                new Line("G1 X16.0697 Y19.1511 Z-0.15"),
                new Line("G2 X21.6506 Y12.5 Z-0.15 I-16.0687 J-19.1501"),
                new Line("M5"),
                new Line("M30"),
            };

            var expectedLinesB = new List<Line> {
                new Line("G17"),
                new Line("G21"),
                new Line("G90"),
                new Line("G1 X0 Y25 Z-0.15"),
                new Line("G2 X21.6506 Y12.5 Z-0.15 I0.0002 J-24.9995"),
                new Line("M5"),
                new Line("M30"),
            };

            var lengthUnits = Utility.GetLengthUnits(Default.Preamble());
            var coordPlane = Default.Preamble().GetModalState(ModalGroup.ModalPlane).ToString();

            // A Test with fine tolerance
            var resultLinesA = await lines.DedupLinearToArc(lengthUnits, coordPlane, 0.005M).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLinesA));
            Assert.True(expectedLinesA.SequenceEqual(resultLinesA));

            // B Test with coarse tolerance
            var resultLinesB = await lines.DedupLinearToArc(lengthUnits, coordPlane, 0.5M).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLinesB));
            Assert.True(expectedLinesB.SequenceEqual(resultLinesB));
        }
    }
}
