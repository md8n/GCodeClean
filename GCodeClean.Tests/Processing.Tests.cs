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
    public class Processing
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public Processing(ITestOutputHelper testOutputHelper)
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
        public async void TestAugment()
        {
            var sourceLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G1 Z-0.15"), new Line("X26.6059 Z - 0.1539 F60"), new Line("X26.6068 Z - 0.1577") };
            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var expectedLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G1 Z-0.15"), new Line("G1 F60 X26.6059 Z-0.1539"), new Line("G1 X26.6068 Z-0.1577") };

            var resultLines = await lines.Augment().ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async void TestZClampMM()
        {
            var sourceLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G0 Z0.15"), new Line("G0 Z0.05"), new Line("G1 Z0.0394 F30"), new Line("G1 Z-0.15") };
            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var setHeight = 1.1M;
            var expectedLines = new List<Line> { new Line("G21"), new Line("G90"), new Line($"G0 Z{setHeight}"), new Line($"G0 Z{setHeight}"), new Line($"G0 Z{setHeight} F30"), new Line("G1 Z-0.15") };

            var resultLines = await lines.ZClamp(Default.Preamble(), setHeight).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async void TestZClampInch()
        {
            var sourceLines = new List<Line> { new Line("G20"), new Line("G90"), new Line("G0 Z0.15"), new Line("G0 Z0.05"), new Line("G1 Z0.0394 F30"), new Line("G1 Z-0.15") };
            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var setHeight = 1.1M;
            var adjustedHeight = 0.5M;
            var expectedLines = new List<Line> { new Line("G20"), new Line("G90"), new Line($"G0 Z{adjustedHeight}"), new Line($"G0 Z{adjustedHeight}"), new Line($"G0 Z{adjustedHeight} F30"), new Line("G1 Z-0.15") };

            var resultLines = await lines.ZClamp(Default.Preamble(), setHeight).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async void TestClip()
        {
            var sourceLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G1 Z0.15678 F9.0") };

            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);

            var resultLines = await lines.Clip(Default.Preamble()).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
        }
    }
}
