// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Text.Json;

namespace Actions.Clean;

public static class CleanOptions {
    //[Option("eliminateNeedlessTravelling", Default = false, HelpText = "Eliminate needless 'travelling', extra movements with positive z-axis values")]
    //public bool EliminateNeedlessTravelling { get; set; }

    public static FileInfo GetCleanTokenDefsPath(this FileInfo tokenDefsPath) {
        if (tokenDefsPath.ToString().Equals("TOKENDEFINITIONS.JSON", StringComparison.InvariantCultureIgnoreCase)) {
            var entryDir = Path.GetDirectoryName(AppContext.BaseDirectory);

            tokenDefsPath = new FileInfo($"{entryDir}{Path.DirectorySeparatorChar}tokenDefinitions.json");
        }
        return tokenDefsPath;
    }

    public static (JsonDocument, string) LoadAndVerifyTokenDefs(this FileInfo tokenDefsPath) {
        JsonDocument tokenDefinitions;

        try {
            var tokenDefsSource = File.ReadAllText(tokenDefsPath.ToString());
            tokenDefinitions = JsonDocument.Parse(tokenDefsSource);
        } catch (FileNotFoundException fileNotFoundEx) {
            return (null, $"No token definitions file was found at {tokenDefsPath}. {fileNotFoundEx.Message}");
        } catch (JsonException jsonEx) {
            return (null, $"The supplied file {tokenDefsPath} does not appear to be valid JSON. {jsonEx.Message}");
        } catch (Exception e) {
            return (null, $"{e}");
        }

        return (tokenDefinitions, "");
    }

    private static decimal ConstrainOption(decimal? option, decimal min, decimal max) {
        var value = min;
        if (option.HasValue) {
            if (option.Value < min) {
                value = min;
            } else if (option.Value > max) {
                value = max;
            } else {
                value = option.Value;
            }
        }

        return value;
    }

    public static (decimal tolerance, decimal arcTolerance, decimal zClamp) Constrain(decimal tolerance, decimal arcTolerance, decimal zClamp) {
        tolerance = ConstrainOption(tolerance, 0.00005M, 0.5M);
        arcTolerance = ConstrainOption(arcTolerance, 0.00005M, 0.5M);
        zClamp = ConstrainOption(zClamp, 0.02M, 10.0M);

        return (tolerance, arcTolerance, zClamp);
    }
}