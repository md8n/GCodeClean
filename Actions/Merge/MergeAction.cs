// Copyright (c) 2023-2025 - Lee HUMPHRIES (lee@md8n.com). All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using GCodeClean.Merge;

namespace Actions.Merge;

public static class MergeAction {
    public static async IAsyncEnumerable<string> ExecuteAsync(DirectoryInfo folder) {
        var inputFolder = folder.ToString();
        yield return $"Inputting from folder: {inputFolder}";

        await foreach(var logMessage in inputFolder.MergeFileAsync()) {
            yield return logMessage;
        }

        yield return "Merge completed";

        yield return "Success";
    }
}
