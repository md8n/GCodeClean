// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using GCodeClean.Processing;
using GCodeClean.Structure;

using Newtonsoft.Json.Linq;

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
        public async void TestClip()
        {
            var sourceLines = new List<Line> { new Line("G21"), new Line("G90"), new Line("G1 Z0.15678 F9.0") };
            var testLines = sourceLines.ConvertAll(l => new Line(l));
            var lines = AsyncLines(testLines);

            var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                           ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var tokenDefsPath = $"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json";

            JObject tokenDefinitions;
            try
            {
                var tokenDefsSource = File.ReadAllText(tokenDefsPath);
                tokenDefinitions = JObject.Parse(tokenDefsSource);
            }
            catch (Exception)
            {
                throw;
            }

            var clippedLines = await lines.Clip(tokenDefinitions).ToListAsync();
            Assert.False(sourceLines.SequenceEqual(clippedLines));
        }
    }
}
