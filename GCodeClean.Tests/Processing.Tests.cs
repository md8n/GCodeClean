// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

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
        public async void TestInjectPreamble()
        {
            var sourceLines = new List<Line> {
                new Line("G17"),
                new Line("G40"),
                new Line("G90"),
                new Line("G21"),
                new Line("T1"),
                new Line("S10000"),
                new Line("M3"),
                new Line("G0 X35.747 Y46.824")
            };
            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);
            var zClamp = 3M;
            var expectedLines = new List<Line> {
                new Line("G17"),
                new Line("G40"),
                new Line("G90"),
                new Line("G21"),
                new Line("T1"),
                new Line("S10000"),
                new Line("M3"),
                new Line("(Preamble completed by GCodeClean)"),
                new Line("G94"),
                new Line("G49"),
                new Line($"Z{zClamp}"),
                new Line("(Preamble completed by GCodeClean)"),
                new Line("G0 X35.747 Y46.824")
            };

            var resultLines = await lines.InjectPreamble(Default.Preamble(), zClamp).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
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

        [Fact]
        public async void TestAnnotate()
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

            var sourceLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G1 Z0.15678 F9.0"), new Line("G1234") };
            var expectedLines = new List<string> {
                "G21 (Length units: Millimeters)",
                "G90 (Set Distance Mode: Absolute)",
                "G1 Z0.1568 F9 (Linear motion: at Feed Rate, Z0.1568mm, Set Feed Rate to {feedRateMode})",
                "G1234"
            };


            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);

            var resultLines = await lines.Annotate(tokenDefinitions.RootElement).JoinLines("SOFT").ToListAsync();

            Assert.True(expectedLines.SequenceEqual(resultLines));
        }
    }
}
