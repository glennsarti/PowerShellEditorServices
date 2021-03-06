//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal class PathUtils
    {
        /// <summary>
        /// The default path separator used by the base implementation of the providers.
        ///
        /// Porting note: IO.Path.DirectorySeparatorChar is correct for all platforms. On Windows,
        /// it is '\', and on Linux, it is '/', as expected.
        /// </summary>
        internal static readonly char DefaultPathSeparator = Path.DirectorySeparatorChar;
        internal static readonly string DefaultPathSeparatorString = DefaultPathSeparator.ToString();

        /// <summary>
        /// The alternate path separator used by the base implementation of the providers.
        ///
        /// Porting note: we do not use .NET's AlternatePathSeparatorChar here because it correctly
        /// states that both the default and alternate are '/' on Linux. However, for PowerShell to
        /// be "slash agnostic", we need to use the assumption that a '\' is the alternate path
        /// separator on Linux.
        /// </summary>
        internal static readonly char AlternatePathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '/' : '\\';
        internal static readonly string AlternatePathSeparatorString = AlternatePathSeparator.ToString();

        public string WildcardUnescapePath(string path)
        {
            throw new NotImplementedException();
        }

        public static Uri ToUri(string filePath)
        {
            if (filePath.StartsWith("untitled", StringComparison.OrdinalIgnoreCase) ||
                filePath.StartsWith("inmemory", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(filePath);
            }

            filePath = filePath.Replace(":", "%3A").Replace("\\", "/");
            if (!filePath.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri($"file:///{filePath}");
            }

            return new Uri($"file://{filePath}");
        }

        public static string FromUri(Uri uri)
        {
            if (uri.Segments.Length > 1)
            {
                // On windows of the Uri contains %3a local path
                // doesn't come out as a proper windows path
                if (uri.Segments[1].IndexOf("%3a", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return FromUri(new Uri(uri.AbsoluteUri.Replace("%3a", ":").Replace("%3A", ":")));
                }
            }
            return uri.LocalPath;
        }

        /// <summary>
        /// Converts all alternate path separators to the current platform's main path separators.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path.</returns>
        public static string NormalizePathSeparators(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace(AlternatePathSeparator, DefaultPathSeparator);
        }
    }
}
