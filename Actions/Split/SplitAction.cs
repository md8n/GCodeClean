// Copyright (c) 2023-2024 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using GCodeClean.IO;
using GCodeClean.Shared;
using GCodeClean.Split;


namespace Actions.Split;

public static class SplitAction {
    private static string DetermineOutputFoldername(this string inputFile) {
        var outputFolderPath = Path.GetDirectoryName(inputFile);
        var outputFolder = Path.GetFileNameWithoutExtension(inputFile);

        return Path.Join(outputFolderPath, outputFolder);
    }

    public static async IAsyncEnumerable<string> ExecuteAsync(FileInfo filename) {
        var inputFile = filename.ToString();

        var outputFolder = inputFile.DetermineOutputFoldername();
        yield return $"Outputting to folder: {outputFolder}";

        var inputLines = inputFile.ReadFileLines();

        var travellingComments = inputLines.GetTravellingComments();
        if (travellingComments.Count == 0) {
            yield return $"File '{inputFile}' has not been pre-processed with the 'Clean' command.";
            yield return "Please choose a different file.";

            yield return "Failure";
            yield break;
        }
        var preambleLines = inputLines.GetPreamble();
        var postambleLines = inputLines.GetPostamble(travellingComments[^1]);

        List<(short seqId, short subSeqId, short nodeId, decimal max, string tool)> rawData = [];

        travellingComments.ForEach(tc => {
            var splitTC = tc.Replace("(||", "").Replace("||)", "").Split("||", StringSplitOptions.None);
            rawData.Add((short.Parse(splitTC[1]), short.Parse(splitTC[2]), short.Parse(splitTC[3]), decimal.Parse(splitTC[4]), splitTC[5]));
        });

        // Get each sequence in order, we'll handle each of these discreetly
        var sequences = rawData.Select(rd => rd.seqId).Distinct().ToList();

        var rawDataMaxes = rawData.Select(rd => rd.max);
        var depthCutMin = rawDataMaxes.Max();
        var depthCutMax = rawDataMaxes.Min();
        var depthCutRange = (depthCutMax - depthCutMin) / 10;
        var depthCutRanges = Enumerable.Range(0, 10).Select(ix => (min: depthCutMin + (depthCutRange * ix), max: depthCutMin + (depthCutRange * (ix + 1)), cnt: 0)).ToList();

        foreach (var sequence in sequences) {
            var seqRawDataMaxes = rawData.Where(rd => rd.seqId == sequence).Select(rd => rd.max);

            for (var ix = 0; ix < depthCutRanges.Count; ix++) {
                var dcr = depthCutRanges[ix];
                // Invert the max and min tests, because we're actually testing furtherest away from zero
                dcr.cnt = seqRawDataMaxes.Count(rd => rd > dcr.max && rd <= dcr.min);
                if (ix == depthCutRanges.Count - 1) {
                    dcr.cnt += seqRawDataMaxes.Count(rd => rd == dcr.max);
                }
                depthCutRanges[ix] = dcr;
            }

            depthCutRanges = depthCutRanges.Where(dcr => dcr.cnt > 0).ToList();
            if (depthCutRanges.Count > 1) {
                for (var ix = depthCutRanges.Count - 2; ix >= 0; ix--) {
                    var dcr = depthCutRanges[ix];
                    // If this particular depth cut range has only one entry, merge the next entry up into it
                    // and remove the next entry
                    if (dcr.cnt == 1) {
                        dcr.cnt += depthCutRanges[ix + 1].cnt;
                        dcr.max = depthCutRanges[ix + 1].max;
                        depthCutRanges[ix] = dcr;
                        depthCutRanges.RemoveAt(ix + 1);
                        // If there's an entry before this one, and that entry has only a single value, then we'll skip over it
                        if (ix > 0 && depthCutRanges[ix - 1].cnt == 1) {
                            ix--;
                        }
                    }
                }
            }

            if (depthCutRanges.Count == 1) {
                // Only one sub sequence, so leave it as-is for each rawData value
                continue;
            }

            for (var ix = 0; ix < rawData.Count; ix++) {
                var rd = rawData[ix];
                if (rd.seqId != sequence) {
                    continue;
                }
                for (short jx = 0; jx < depthCutRanges.Count; jx++) {
                    var (min, max, _) = depthCutRanges[jx];
                    if (rd.max > max && rd.max < min) {
                        rd.subSeqId = jx;
                        rawData[ix] = rd;
                    }
                    if (jx == depthCutRanges.Count - 1 && rd.max == max) {
                        rd.subSeqId = jx;
                        rawData[ix] = rd;
                    }
                }
            }
        }


        depthCutRanges = depthCutRanges.Where(dcr => dcr.cnt > 0).ToList();
        if (depthCutRanges.Count > 1) {
            for (var ix = depthCutRanges.Count - 2; ix >= 0; ix--) {
                var dcr = depthCutRanges[ix];
                // If this particular depth cut range has only one entry, merge the next entry up into it
                // and remove the next entry
                if (dcr.cnt == 1) {
                    dcr.cnt += depthCutRanges[ix + 1].cnt;
                    dcr.max = depthCutRanges[ix + 1].max;
                    depthCutRanges[ix] = dcr;
                    depthCutRanges.RemoveAt(ix + 1);
                    // If there's an entry before this one, and that entry has only a single value, then we'll skip over it
                    if (ix > 0 && depthCutRanges[ix - 1].cnt == 1) {
                        ix--;
                    }
                }
            }
        }
        if (depthCutRanges.Count > 0) {
            // Redo the sub sequence value in the travelling comments
            for (var ix = 0; ix < travellingComments.Count; ix++) {
                var node = travellingComments[ix].ToNode();
                short subSeqIx = -1;
                for (short jx = 0; jx < depthCutRanges.Count; jx++) {
                    var (min, max, _) = depthCutRanges[jx];
                    if (node.MaxZ > max && node.MaxZ < min) {
                        subSeqIx = jx;
                        break;
                    }
                    if (jx == depthCutRanges.Count - 1 && node.MaxZ == max) {
                        subSeqIx = jx;
                        break;
                    }
                }
                if (subSeqIx == -1) {
                    // For some reason we hit the failsafe, so use a failsafe value
                    subSeqIx = (short)(node.MaxZ > depthCutRanges[0].max ? 0 : depthCutRanges.Count - 1);
                }
                var subNode = node.CopySetSub(subSeqIx);
                travellingComments[ix] = subNode.ToTravelling();
            }
        }

        await foreach(var logMessage in inputLines.SplitFile(outputFolder, travellingComments, preambleLines, postambleLines)) {
            yield return logMessage;
        }

        yield return "Split completed";
        yield return "Success";
    }
}