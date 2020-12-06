// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
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
        public async void DedupLinearToArc()
        {
            var sourceLines = new List<Line> {
                new Line("G17"),
                new Line("G21"),
                new Line("G90"),
                new Line("G1 Z-0.15"),
                new Line("G1 X24.4773 Y24.4129"),
                new Line("G1 X24.3427 Y24.8798"),
                new Line("G1 X24.5126 Y24.9612"),
                new Line("G1 X25.7214 Y25.8103"),
                new Line("G1 X24.9402 Y27.4502"),
                new Line("G1 X23.8103 Y27.1292"),
                new Line("G1 X22.8441 Y27.4596"),
                new Line("G1 X22.1773 Y26.0646"),
                new Line("G1 X21.8448 Y25.6385"),
                new Line("G1 X21.8361 Y25.3508"),
                new Line("G1 X21.7890 Y25.2523"),
                new Line("G1 X21.8321 Y25.2170"),
                new Line("G1 X21.7860 Y23.6946"),
                new Line("M5"),
                new Line("M30"),
            };

            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var expectedLines = new List<Line> {
                new Line("G17"),
                new Line("G21"),
                new Line("G90"),
                new Line("G1 Z-0.15"),
                new Line("G1 X24.4773 Y24.4129"),
                new Line("G1 X24.3427 Y24.8798"),
                new Line("G3 X25.7214 Y25.8103 I-2.095 J4.5907"),
                new Line("G1 X24.9402 Y27.4502"),
                new Line("G1 X23.8103 Y27.1292"),
                new Line("G1 X22.8441 Y27.4596"),
                new Line("G2 X21.8448 Y25.6385 I-4.6347 J1.3585"),
                new Line("G1 X21.8361 Y25.3508"),
                new Line("G1 X21.7890 Y25.2523"),
                new Line("G1 X21.8321 Y25.217"),
                new Line("G1 X21.7860 Y23.6946"),
                new Line("M5"),
                new Line("M30"),
            };

            var resultLines = await lines.DedupLinearToArc(Default.Preamble(), 0.5M).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }
    }
}
