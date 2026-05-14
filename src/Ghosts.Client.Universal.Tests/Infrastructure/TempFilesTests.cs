// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
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
}
