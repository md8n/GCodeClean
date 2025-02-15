// Copyright (c) 2020-2025 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the AGPL license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GCodeClean.IO;

public static class AsyncTextFile
{
    private const int DefaultBufferSize = 4096;
    private const FileOptions DefaultOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;

    /// <summary>
    /// Reads and yields lines one at a time from the input source file
    /// </summary>
    /// <param name="path">Path to the input file</param>
    /// <exception cref="FileNotFoundException">Throws FileNotFoundException if the input source file can not be found</exception>
    /// <returns></returns>
    public static async IAsyncEnumerable<string> ReadLinesAsync(this string path)
    {
        var encoding = Encoding.UTF8;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, DefaultOptions);
        using var reader = new StreamReader(stream, encoding);
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            yield return line;
        }
    }

    public static async IAsyncEnumerable<int> WriteLinesAsync(this string path, IAsyncEnumerable<string> lines)
    {
        var encoding = Encoding.UTF8;
        var counter = 0;

        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, DefaultBufferSize, DefaultOptions))
        await using (var writer = new StreamWriter(stream, encoding))
        {
            await foreach (var line in lines)
            {
                writer.WriteLine(line);
                counter++;
            }
        }

        yield return counter;
    }
}
