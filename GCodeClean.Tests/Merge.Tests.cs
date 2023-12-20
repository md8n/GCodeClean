// Copyright (c) 2023 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

using GCodeClean.Merge;


namespace GCodeClean.Tests {
    public class MergeTest(ITestOutputHelper testOutputHelper) {
        [Fact]
        public void TestCheckForLoopsFirstPairings() {
            List<Edge> sourceEdges = [
                new Edge(0, 1, 0, 0),
                new Edge(1, 2, 98.1337309440541M, 4),
                new Edge(2, 0, 94.0106166876912M, 4),
                new Edge(3, 4, 14.2090530296709M, 1),
                new Edge(4, 5, 28.7462839511475M, 2),
                new Edge(5, 6, 0, 0),
                new Edge(6, 7, 22.3672135278403M, 1),
                new Edge(7, 3, 47.4712088007036M, 3),
                new Edge(8, 9, 0, 0),
                new Edge(9, 10, 28.7073332268952M, 1),
                new Edge(10, 11, 0, 0),
                new Edge(11, 12, 0, 0),
                new Edge(12, 8, 30.127148371527M, 1),
                new Edge(13, 14, 0, 0),
                new Edge(14, 15, 25.1760346361376M, 1),
                new Edge(15, 16, 47.586326292329M, 2),
                new Edge(16, 17, 0, 0),
                new Edge(17, 18, 14.2227357776203M, 1),
                new Edge(18, 19, 25.4259792338466M, 1),
                new Edge(19, 13, 155.649088105263M, 4),
            ];
            var pairedEdges = sourceEdges.CheckForLoops();

            Assert.True(sourceEdges.Count == 20);
            Assert.True(pairedEdges[16].Weighting == 100); // new Edge(2, 0, 94.0106166876912M, 4),
            Assert.True(pairedEdges[17].Weighting == 100); // new Edge(7, 3, 47.4712088007036M, 3),
            Assert.True(pairedEdges[18].Weighting == 100); // new Edge(12, 8, 30.127148371527M, 1),
            Assert.True(pairedEdges.Count(pe => pe.Weighting == 100) == 4);
        }

        [Fact]
        public void TestCheckForLoopsProblemPairings() {
            List<Edge> sourceEdges = [
                new Edge(3, 2, 35.630983048465M, 1),
                new Edge(4, 3, 1.79479943169146M, 1),
                new Edge(3, 4, 14.2090530296709M, 1),
                new Edge(3, 5, 24.6702927627542M, 1),
                new Edge(6, 7, 22.3672135278403M, 1),
                new Edge(12, 8, 30.127148371527M, 1),
                new Edge(9, 10, 28.7073332268952M, 1),
                new Edge(14, 15, 25.1760346361376M, 1),
                new Edge(15, 13, 0.219672483483936M, 1),
                new Edge(15, 16, 47.586326292329M, 1),
                new Edge(2, 0, 94.0106166876912M, 1),
                new Edge(17, 18, 14.2227357776203M, 1),
                new Edge(18, 19, 25.4259792338466M, 1),
            ];
            var pairedEdges = sourceEdges.CheckForLoops();

            Assert.True(sourceEdges.Count == 13);
            Assert.True(pairedEdges.Count == 13);
            Assert.True(pairedEdges.Count(pe => pe.Weighting == 100) == 3);
        }

        [Fact]
        public void TestCheckForLoopsLateFork() {
            List<Edge> sourceEdges = [
                new Edge(14, 15, 25.1760346361376M, 10),
                new Edge(15, 13, 0.219672483483936M, 10),
                new Edge(4, 3, 1.79479943169146M, 10),
                new Edge(17, 18, 14.2227357776203M, 10),
                new Edge(18, 19, 25.4259792338466M, 10),
                new Edge(6, 7, 22.3672135278403M, 10),
                new Edge(7, 5, 39.6719488429797M, 10),
                new Edge(9, 10, 28.7073332268952M, 10),
                new Edge(12, 8, 30.127148371527M, 10),
                new Edge(1, 2, 98.1337309440541M, 10),
                new Edge(2, 3, 50.7771087105203M, 10),
                new Edge(3, 4, 14.2090530296709M, 10),
                new Edge(19, 18, 76.3650198061914M, 10),
            ];
            var pairedEdges = sourceEdges.CheckForLoops();

            Assert.True(sourceEdges.Count == 13);
            Assert.True(pairedEdges.Count == 13);
            Assert.True(pairedEdges.Count(pe => pe.Weighting == 100) == 3);
        }

        [Fact]
        public void TestFilterEdgePairs() {
            List<Edge> sourceEdges = [
                new Edge(3, 2, 35.630983048465M, 1),
                new Edge(4, 3, 1.79479943169146M, 1),
                new Edge(3, 4, 14.2090530296709M, 1),
                new Edge(3, 5, 24.6702927627542M, 1),
                new Edge(6, 7, 22.3672135278403M, 1),
                new Edge(12, 8, 30.127148371527M, 1),
                new Edge(9, 10, 28.7073332268952M, 1),
                new Edge(14, 15, 25.1760346361376M, 1),
                new Edge(15, 13, 0.219672483483936M, 1),
                new Edge(15, 16, 47.586326292329M, 1),
                new Edge(2, 0, 94.0106166876912M, 1),
                new Edge(17, 18, 14.2227357776203M, 1),
                new Edge(18, 19, 25.4259792338466M, 1),
            ];
            var filteredEdges = sourceEdges.FilterEdgePairs();

            Assert.True(sourceEdges.Count == 13);
            Assert.True(filteredEdges.Count == 10);
            Assert.False(filteredEdges.Exists(fe => fe.Weighting == 100));
        }

        [Fact]
        public void TestFilterEdgePairsWithCurrentPairs() {
            List<Edge> sourceEdges = [
                new Edge(2, 0, 94.0106166876912M, 1),
                new Edge(3, 4, 14.2090530296709M, 1),
                new Edge(3, 5, 24.6702927627542M, 1),
                new Edge(4, 3, 1.79479943169146M, 1),
                new Edge(6, 7, 22.3672135278403M, 1),
                new Edge(9, 10, 28.7073332268952M, 1),
                new Edge(12, 8, 30.127148371527M, 1),
                new Edge(14, 15, 25.1760346361376M, 1),
                new Edge(15, 13, 0.219672483483936M, 1),
                new Edge(15, 16, 47.586326292329M, 1),
                new Edge(17, 18, 14.2227357776203M, 1),
                new Edge(18, 19, 25.4259792338466M, 1),
            ];
            List<Edge> currentEdges = [
                new Edge(0, 1, 0, 0),
                new Edge(5, 6, 0, 0),
                new Edge(8, 9, 0, 0),
                new Edge(10, 11, 0, 0),
                new Edge(11, 12, 0, 0),
                new Edge(13, 14, 0, 0),
                new Edge(16, 17, 0, 0),
            ];

            var pairedEdges = sourceEdges.FilterEdgePairsWithCurrentPairs(currentEdges);

            Assert.True(sourceEdges.Count == 12);
            Assert.True(currentEdges.Count == 7);
            Assert.True(pairedEdges.Count == 8);
            Assert.False(pairedEdges.Exists(pe => pe.Weighting == 100));
        }
    }
}
