// Copyright (c) 2020-2023 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GCodeClean.IO
{
    public static class TextFile
    {
        /// <summary>
        /// Opens the input source file and returns an IEnumerable of the lines. Dispose of the IEnumerable to close the file
        /// </summary>
        /// <param name="path">Path to the input file</param>
        /// <exception cref="FileNotFoundException">Throws FileNotFoundException if the input source file can not be found</exception>
        /// <returns></returns>
        public static IEnumerable<string> ReadFileLines(this string path)
        {
            var encoding = Encoding.UTF8;

            return File.ReadLines(path, encoding);
        }
    }
}
