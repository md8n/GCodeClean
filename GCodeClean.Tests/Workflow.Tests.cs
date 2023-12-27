// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
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
    public class Workflow(ITestOutputHelper testOutputHelper) {
        private static async IAsyncEnumerable<Line> AsyncLines(IEnumerable<Line> lines)
        {
            foreach (var line in lines)
            {
                await Task.Delay(1);
                yield return line;
            }
        }

        [Fact]
        public async Task CleanLinesFirstPhase() {
            List<string> sourceTextLines = [
                "G21", "G90", "G94", "G17", "G40", "G49", "G54", "M3",
                "G00 Z1.5",
                "G00 X14.7236 Y97.7144",
                "G00 Z0.5000",
                "G01 Z-1.1350",
                "G00 Z0.5000",
                "G00 Z1.5000",
                "G00 X54.0331 Y136.0945",
                "G01 Z-0.2492",
                "G01 X54.1250 Y136.1674 Z-0.3065",
                "G01 X54.1775 Y136.2112 Z-0.3391",
            ];
            var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
            var sourceLines = sourceTextLines.ToAsyncEnumerable();
            
            List<Line> expectedLines = [
                new Line("G0 Z1.5"),
                new Line("G0 X14.7236 Y97.7144 Z1.5"),
                new Line("G0 X14.7236 Y97.7144 Z0.5"),
                new Line("G1 X14.7236 Y97.7144 Z-1.135"),
                new Line("G0 X14.7236 Y97.7144 Z0.5"),
                new Line("G0 X14.7236 Y97.7144 Z1.5"),
                new Line("G0 X54.0331 Y136.0945 Z1.5"),
                new Line("G1 X54.0331 Y136.0945 Z-0.2492"),
                new Line("G1 X54.125 Y136.1674 Z-0.3065"),
                new Line("G1 X54.1775 Y136.2112 Z-0.3391"),
            ];

            // The preamble context effectively gets stripped out, and added back in with the PreAndPostAmblePhase
            var resultLines = await sourceLines.CleanLinesFirstPhase(false).ToListAsync();
            Assert.False(sourceLineLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async Task PreAndPostAmblePhase() {
            List<string> sourceTextLines = [
                "G17",
                "G40",
                "G90",
                "G21",
                "G94",
                "G49",
                "G54",
                "M3",
                "G00 Z1.5",
                "G00 X68.904 Y128.746 Z1.5",
                "G01 X68.904 Y128.746 Z-1.194",
                "G01 X68.995 Y128.814 Z-1.254",
                "G01 X69.089 Y128.892 Z-1.322",
                "G00 Z1.5",
                "G00 X42.239 Y157.031",
                "G01 Z-0.413",
                "G01 X42.33 Y157.498 Z-0.468",
            ];
            var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
            var sourceLines = sourceTextLines.ToAsyncEnumerable();

            List<Line> expectedLines = [
                new Line(Default.PreambleCompletion),
                new Line("G21"),
                new Line("G90"),
                new Line("G94"),
                new Line("G17"),
                new Line("G40"),
                new Line("G49"),
                new Line("G54"),
                new Line("M3"),
                new Line(Default.PreambleCompleted),
                new Line(""),
                new Line("G0 Z0.5"),
                new Line("G0 X68.904 Y128.746 Z1.5"),
                new Line("G1 X68.904 Y128.746 Z-1.194"),
                new Line("G1 X68.995 Y128.814 Z-1.254"),
                new Line("G1 X69.089 Y128.892 Z-1.322"),
                new Line("G0 X69.089 Y128.892 Z1.5"),
                new Line("G0 X42.239 Y157.031 Z1.5"),
                new Line("G1 X42.239 Y157.031 Z-0.413"),
                new Line("G1 X42.33 Y157.498 Z-0.468"),
                new Line("G0 Z0.5"),
                new Line(Default.PostAmbleCompleted),
                new Line("M30"),
            ];

            var firstPhaseLines = sourceLines.CleanLinesFirstPhase(false);
            var preambleContext = await firstPhaseLines.BuildPreamble();

            var resultLines = await firstPhaseLines.PreAndPostamblePhase(preambleContext, 0.5M).ToListAsync();
            Assert.False(sourceLineLines.SequenceEqual(resultLines));
            Assert.True(expectedLines.SequenceEqual(resultLines));
        }

        [Fact]
        public async Task CleanLinesSecondPhase() {
            List<string> sourceTextLines = [
                "G17",
                "G40",
                "G90",
                "G21",
                "G94",
                "G49",
                "G54",
                "M3",
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
            ];

            var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
            var sourceLines = sourceTextLines.ToAsyncEnumerable();

            List<Line> expectedLines = [
                new Line(Default.PreambleCompletion),
                new Line("G21"),
                new Line("G90"),
                new Line("G94"),
                new Line("G17"),
                new Line("G40"),
                new Line("G49"),
                new Line("G54"),
                new Line("M3"),
                new Line(Default.PreambleCompleted),
                new Line(""),

                new Line("G0 Z0.5"),
                new Line("G0 X54.178 Y136.211 Z0.5"),
                new Line("G1 X54.178 Y136.211 Z-0.678"),
                new Line("G1 X54.033 Y136.095 Z-0.499"),
                new Line("G0 X54.033 Y136.095 Z0.5 (||Travelling||notset||0||>>G0 X54.178 Y136.211 Z0.5>>G0 X54.033 Y136.095 Z0.5>>||)"),

                new Line("G0 X69.089 Y128.892 Z0.5"),
                new Line("G1 X69.089 Y128.892 Z-0.661"),
                new Line("G1 X68.813 Y128.684 Z-0.57"),
                new Line("G0 X68.813 Y128.684 Z0.5 (||Travelling||notset||1||>>G0 X69.089 Y128.892 Z0.5>>G0 X68.813 Y128.684 Z0.5>>||)"),

                new Line(Default.PostAmbleCompleted),
                new Line("M30"),
            ];

            var eliminateNeedlessTravel = true;
            decimal zClamp = 0.5M;
            decimal arcTolerance = 0.0005M;
            decimal tolerance = 0.0005M;

            var firstPhaseLines = sourceLines.CleanLinesFirstPhase(false);
            var preambleContext = await firstPhaseLines.BuildPreamble();
            var preAndPostamblePhaseLines = firstPhaseLines.PreAndPostamblePhase(preambleContext, zClamp);

            var resultLines = await preAndPostamblePhaseLines.CleanLinesSecondPhase(eliminateNeedlessTravel, zClamp, arcTolerance, tolerance).ToListAsync();

            Assert.False(sourceLineLines.SequenceEqual(resultLines));
            for (var ix = 0; ix < expectedLines.Count && ix < resultLines.Count; ix++) {
                var expected = expectedLines[ix];
                var result = resultLines[ix];
                Assert.True(expected == result);
            }
            Assert.True(expectedLines.Count == resultLines.Count);
        }


        [Fact]
        public async Task CleanLinesThirdPhase() {
            var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";

            JsonDocument tokenDefinitions;
            try {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
            } catch (FileNotFoundException fileNotFoundEx) {
                Console.WriteLine($"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
                return;
            } catch (JsonException jsonEx) {
                Console.WriteLine($"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}");
                return;
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }

            List<string> sourceTextLines = [
                "G17",
                "G40",
                "G90",
                "G21",
                "G94",
                "G49",
                "G54",
                "M3",
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
            ];
            var sourceLineLines = sourceTextLines.ConvertAll(l => new Line(l));
            var sourceLines = sourceTextLines.ToAsyncEnumerable();

            List<Line> expectedLines = [
                new Line(Default.PreambleCompletion),
                new Line("G21"),
                new Line("G90"),
                new Line("G94"),
                new Line("G17"),
                new Line("G40"),
                new Line("G49"),
                new Line("G54"),
                new Line("M3"),
                new Line(Default.PreambleCompleted),
                new Line(""),
                new Line("G0 Z0.5"),

                new Line("G0 X54.178 Y136.211"),
                new Line("G1 X54.178 Y136.211 Z-0.678"),
                new Line("G1 X54.033 Y136.095 Z-0.499"),
                new Line("G0 X54.033 Y136.095 Z0.5 (||Travelling||notset||0||>>G0 X54.178 Y136.211 Z0.5>>G0 X54.033 Y136.095 Z0.5>>||)"),

                new Line("G0 X69.089 Y128.892"),
                new Line("G1 X69.089 Y128.892 Z-0.661"),
                new Line("G1 X68.813 Y128.684 Z-0.57"),
                new Line("G0 X68.813 Y128.684 Z0.5 (||Travelling||notset||1||>>G0 X69.089 Y128.892 Z0.5>>G0 X68.813 Y128.684 Z0.5>>||)"),

                new Line(Default.PostAmbleCompleted),
                new Line("M30"),
            ];

            var eliminateNeedlessTravel = true;
            decimal zClamp = 0.5M;
            decimal arcTolerance = 0.0005M;
            decimal tolerance = 0.0005M;
            List<char> dedupSelection = [Letter.feedRate, 'Z'];

            var firstPhaseLines = sourceLines.CleanLinesFirstPhase(false);
            var preambleContext = await firstPhaseLines.BuildPreamble();
            var preAndPostamblePhaseLines = firstPhaseLines.PreAndPostamblePhase(preambleContext, zClamp);
            var secondPhaseLines = preAndPostamblePhaseLines.CleanLinesSecondPhase(eliminateNeedlessTravel, zClamp, arcTolerance, tolerance);

            var resultLines = await secondPhaseLines.CleanLinesThirdPhase(dedupSelection, false, tokenDefinitions).ToListAsync();
            Assert.False(sourceLineLines.SequenceEqual(resultLines));
            for (var ix = 0; ix < expectedLines.Count && ix < resultLines.Count; ix++) {
                var expected = expectedLines[ix];
                var result = resultLines[ix];
                Assert.True(expected == result);
            }
            Assert.True(expectedLines.Count == resultLines.Count);
        }
    }
}
