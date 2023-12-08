// Copyright (c) 2021 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using GCodeClean.Structure;

using Xunit;
using Xunit.Abstractions;

namespace GCodeClean.Tests
{
    public class LineTest(ITestOutputHelper testOutputHelper) {
        [Fact]
        public void TestParseString() {
            var sourceLine = "/  G01 N33 (Penetrate) X803.195 #54=-4 Y317.845 Z#54 (and Cut)";
            var sourceTokenisedLine = new Line(sourceLine);
            var expectedLine = "/ N33 G1 X803.195 #54=-4 Y317.845 Z#54 (Penetrate) (and Cut)";

            var resultLine = sourceTokenisedLine.ToString();
            Assert.False(sourceLine == resultLine);
            Assert.True(expectedLine == resultLine);
        }

        [Fact]
        public void TestToString()
        {
            var sourceLine = "  G01 N33 (Penetrate) X803.195 #54=-4 Y317.845 Z#54 (and Cut)";
            var sourceTokenisedLine = new Line(sourceLine);
            var expectedLine = "N33 G1 X803.195 #54=-4 Y317.845 Z#54 (Penetrate) (and Cut)";

            var resultLine = sourceTokenisedLine.ToString();
            Assert.False(sourceLine == resultLine);
            Assert.True(expectedLine == resultLine);
        }
    }
}
