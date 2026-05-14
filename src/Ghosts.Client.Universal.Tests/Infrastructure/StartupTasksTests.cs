// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Client.Universal.Infrastructure;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Infrastructure;

public class StartupTasksTests
{
    [Fact]
    public void CleanupProcesses_DoesNotThrow_WhenNoTimeline()
    {
        // CleanupProcesses should handle the case where no timeline is available
        var ex = Record.Exception(() => StartupTasks.CleanupProcesses());
        Assert.Null(ex);
    }

    [Fact]
    public void SetStartup_IsNoOp_DoesNotThrow()
    {
        var ex = Record.Exception(() => StartupTasks.SetStartup());
        Assert.Null(ex);
    }
}
