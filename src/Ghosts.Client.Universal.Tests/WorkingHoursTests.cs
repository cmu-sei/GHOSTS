// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Xunit;

namespace Ghosts.Client.Universal.Tests;

public class WorkingHoursTests
{
    [Fact]
    public void Is_BeforeTimeOn_RunsBeforeSleepHookBeforeSleeping()
    {
        var now = DateTime.UtcNow;
        var handler = new TimelineHandler
        {
            UtcTimeOn = now.TimeOfDay + TimeSpan.FromMilliseconds(300),
            UtcTimeOff = now.TimeOfDay + TimeSpan.FromHours(1)
        };
        DateTime? hookRanAt = null;
        WorkingHours.BeforeSleep = h => { if (h == handler) hookRanAt = DateTime.UtcNow; };

        try
        {
            WorkingHours.Is(handler);
        }
        finally
        {
            WorkingHours.BeforeSleep = null;
        }

        Assert.NotNull(hookRanAt);
        Assert.True(hookRanAt < now.Date + handler.UtcTimeOn, "hook should run before the sleep, not after it");
    }

    [Fact]
    public void Is_WithUnsetHours_DoesNotRunBeforeSleepHook()
    {
        var handler = new TimelineHandler();
        var hookRan = false;
        WorkingHours.BeforeSleep = h => { if (h == handler) hookRan = true; };

        try
        {
            WorkingHours.Is(handler);
        }
        finally
        {
            WorkingHours.BeforeSleep = null;
        }

        Assert.False(hookRan);
    }
}
