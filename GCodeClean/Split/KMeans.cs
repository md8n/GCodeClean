using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeClean.Split {
    public static class KMeans {
        public static int[] Cluster(this List<List<decimal>> rawData, int numClusters) {
            // k-means clustering
            // index of return is tuple ID, cell is cluster ID
            // ex: [2 1 0 0 2 2] means tuple 0 is cluster 2, tuple 1 is cluster 1, tuple 2 is cluster 0, tuple 3 is cluster 0, etc.
            // an alternative clustering DS to save space is to use the .NET BitArray class
            var data = rawData.Normalised(); // so large values don't dominate

            bool changed = true; // was there a change in at least one cluster assignment?
            bool success = true; // were all means able to be computed? (no zero-count clusters)

            // init clustering[] to get things started
            int[] clustering = InitClustering(data.Count, numClusters, 0); // semi-random initialization
            var means = Allocate(numClusters, data[0].Count); // small convenience

            int maxCount = data.Count * 10; // sanity check
            int ct = 0;
            while (changed && success && ct < maxCount) {
                ct++;
                success = UpdateMeans(data, clustering, means);
                changed = UpdateClustering(data, clustering, means);
            }
            return clustering;
        }

        private static List<List<decimal>> Normalised(this List<List<decimal>> rawData) {
            // normalize raw data by computing (x - mean) / stddev
            // primary alternative is min-max:
            // v' = (v - min) / (max - min)

            // make a copy of input data
            var result = rawData.Select(rd => rd.Select(r => r).ToList()).ToList();

            for (var jx = 0; jx < result[0].Count; jx++) {
                // each col
                var colSum = 0.0M;
                for (var ix = 0; ix < result.Count; ix++) {
                    colSum += result[ix][jx];
                }

                var mean = colSum / result.Count;
                var sum = 0.0M;
                for (var ix = 0; ix < result.Count; ix++) {
                    var subMean = result[ix][jx] - mean;
                    sum += (subMean * subMean);
                }

                var sd = sum / result.Count;
                for (var ix = 0; ix < result.Count; ix++) {
                    result[ix][jx] = (result[ix][jx] - mean) / sd;
                }
            }

            return result;
        }

        private static int[] InitClustering(int numTuples, int numClusters, int randomSeed) {
            // init clustering semi-randomly (at least one tuple in each cluster)
            Random random = new Random(randomSeed);
            var clustering = new int[numTuples];
            for (var ix = 0; ix < numClusters; ix++) {
                // make sure each cluster has at least one tuple
                clustering[ix] = ix;
            }
            for (var ix = numClusters; ix < clustering.Length; ix++) {
                clustering[ix] = random.Next(0, numClusters); // other assignments random
            }

            return clustering;
        }

        private static List<List<decimal>> Allocate(int numClusters, int numColumns) {
            // convenience matrix allocator for Cluster()
            var result = new List<List<decimal>>();
            for (var ix = 0; ix < numClusters; ix++) {
                var res = new List<decimal>(numColumns);
                for (var jx = 0; jx < numColumns; jx++) {
                    res.Add(0);
                }
                result.Add(res);
            }
            return result;
        }

        private static bool UpdateMeans(List<List<decimal>> data, int[] clustering, List<List<decimal>> means) {
            // returns false if there is a cluster that has no tuples assigned to it

            // check existing cluster counts
            // can omit this check if InitClustering and UpdateClustering
            // both guarantee at least one tuple in each cluster (usually true)
            var numClusters = means.Count;
            var clusterCounts = new int[numClusters];
            for (var ix = 0; ix < data.Count; ix++) {
                var cluster = clustering[ix];
                clusterCounts[cluster]++;
            }

            for (var kx = 0; kx < numClusters; kx++) {
                if (clusterCounts[kx] == 0) {
                    return false; // bad clustering. no change to means
                }
            }

            // update, zero-out means so it can be used as scratch matrix 
            for (var kx = 0; kx < means.Count; kx++) {
                for (var jx = 0; jx < means[kx].Count; jx++) {
                    means[kx][jx] = 0.0M;
                }
            }

            for (var ix = 0; ix < data.Count; ix++) {
                var cluster = clustering[ix];
                for (var jx = 0; jx < data[ix].Count; jx++) {
                    means[cluster][jx] += data[ix][jx]; // accumulate sum
                }
            }

            for (var kx = 0; kx < means.Count; kx++) {
                for (var jx = 0; jx < means[kx].Count; jx++) {
                    means[kx][jx] /= clusterCounts[kx]; // danger of div by 0
                }
            }

            return true;
        }

        private static bool UpdateClustering(List<List<decimal>> data, int[] clustering, List<List<decimal>> means) {
            // (re)assign each tuple to a cluster (closest mean)
            // returns false if no tuple assignments change OR
            // if the reassignment would result in a clustering where
            // one or more clusters have no tuples.

            var numClusters = means.Count;
            var changed = false;

            var newClustering = new int[clustering.Length]; // proposed result
            Array.Copy(clustering, newClustering, clustering.Length);

            var distances = new List<decimal>(numClusters); // distances from curr tuple to each mean

            for (var ix = 0; ix < data.Count; ix++) // walk thru each tuple
            {
                for (var kx = 0; kx < numClusters; kx++) { 
                    distances[kx] = Distance(data[ix], means[kx]); // compute distances from curr tuple to all k means
                }

                var newClusterID = distances.MinIndex(); // find closest mean ID
                if (newClusterID != newClustering[ix]) {
                    changed = true;
                    newClustering[ix] = newClusterID; // update
                }
            }

            if (!changed) { 
                return false; // no change so bail and don't update clustering
            }
   
            // check proposed clustering[] cluster counts
            var clusterCounts = new int[numClusters];
            for (var ix = 0; ix < data.Count; ix++) {
                var cluster = newClustering[ix];
                clusterCounts[cluster]++;
            }

            for (var kx = 0; kx < numClusters; kx++) {
                if (clusterCounts[kx] == 0) {
                    return false; // bad clustering. no change to clustering
                }
            }

            Array.Copy(newClustering, clustering, newClustering.Length); // update
            return true; // good clustering and at least one change
        }

        private static decimal Distance(List<decimal> tuple, List<decimal> mean) {
            double sumSquaredDiffs = 0.0;
            for (var jx = 0; jx < tuple.Count; jx++) { 
                sumSquaredDiffs += Math.Pow((double)(tuple[jx] - mean[jx]), 2);
            }
            return (decimal)Math.Sqrt(sumSquaredDiffs);
        }

        /// <summary>
        /// Index of smallest value in List
        /// </summary>
        /// <param name="distances"></param>
        /// <returns></returns>
        private static int MinIndex(this List<decimal> distances) {
            var minDist = distances.Min();
            return distances.IndexOf(minDist);
        }
    }
}
