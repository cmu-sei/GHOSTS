// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;

namespace ghosts.api.Infrastructure.Extensions;

public static class DirectoryExtensions
{
    public static bool IsPathWithinAppScope(this string targetPath, string root)
    {
        try
        {
            var fullRootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullTargetPath = Path.GetFullPath(Path.Combine(fullRootPath, targetPath));

            return fullTargetPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
