// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Infrastructure;

public class TempFilesTests
{
    [Fact]
    public void TempFolder_Exists()
    {
        var tempPath = Path.GetTempPath();
        Assert.True(Directory.Exists(tempPath));
    }

    [Fact]
    public void TempFiles_Class_Exists()
    {
        // Verify the TempFiles class is accessible and has the expected API
        var type = typeof(Ghosts.Client.Universal.Infrastructure.TempFiles);
        var method = type.GetMethod("StartTempFileWatcher");
        Assert.NotNull(method);
        Assert.True(method.IsStatic);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void GhostsTempPath_IsScopedSubfolderOfSystemTemp()
    {
        var path = Ghosts.Client.Universal.Infrastructure.TempFiles.GetGhostsTempPath();

        Assert.Equal(Path.Combine(Path.GetTempPath(), "ghosts"), path);
        Assert.NotEqual(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            path.TrimEnd(Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void CleanUp_DeletesAgedEntries_AndSparesRecentOnes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ghosts-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var agedFile = Path.Combine(root, "aged.txt");
            var recentFile = Path.Combine(root, "recent.txt");
            var agedDir = Path.Combine(root, "aged-dir");
            var recentDir = Path.Combine(root, "recent-dir");

            File.WriteAllText(agedFile, "aged");
            File.WriteAllText(recentFile, "recent");
            Directory.CreateDirectory(agedDir);
            Directory.CreateDirectory(recentDir);

            var old = DateTime.UtcNow.AddMinutes(-120);
            File.SetLastWriteTimeUtc(agedFile, old);
            Directory.SetLastWriteTimeUtc(agedDir, old);

            var cleanUp = typeof(Ghosts.Client.Universal.Infrastructure.TempFiles)
                .GetMethod("CleanUpTempFolder", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(cleanUp);
            cleanUp.Invoke(null, new object[] { root, 60 });

            Assert.False(File.Exists(agedFile));
            Assert.False(Directory.Exists(agedDir));
            Assert.True(File.Exists(recentFile));
            Assert.True(Directory.Exists(recentDir));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
