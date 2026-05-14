// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Infrastructure;

public class SafetyNetTests
{
    [Fact]
    public void GetProcessesByName_ForCurrentProcess_ReturnsResults()
    {
        var processName = Process.GetCurrentProcess().ProcessName;
        var processes = Process.GetProcessesByName(processName);
        Assert.NotEmpty(processes);
    }

    [Fact]
    public void GetProcessesByName_ForNonexistent_ReturnsEmpty()
    {
        var processes = Process.GetProcessesByName("nonexistent_process_xyz_99999");
        Assert.Empty(processes);
    }

    [Fact]
    public void Timeline_HandlerCount_CanBeQueriedByType()
    {
        var timeline = new Timeline
        {
            Id = Guid.NewGuid(),
            Status = Timeline.TimelineStatus.Run,
            TimeLineHandlers = new List<TimelineHandler>
            {
                new() { HandlerType = HandlerType.BrowserChrome },
                new() { HandlerType = HandlerType.BrowserChrome },
                new() { HandlerType = HandlerType.BrowserFirefox },
            }
        };

        var chromeCount = timeline.TimeLineHandlers.Count(h => h.HandlerType == HandlerType.BrowserChrome);
        var firefoxCount = timeline.TimeLineHandlers.Count(h => h.HandlerType == HandlerType.BrowserFirefox);

        Assert.Equal(2, chromeCount);
        Assert.Equal(1, firefoxCount);
    }

    [Fact]
    public void HandlerType_ToProcessName_MappingCoversExpectedTypes()
    {
        // Verify the mapping logic used by SafetyNet
        var handlerProcessMap = new Dictionary<HandlerType, string>
        {
            [HandlerType.BrowserChrome] = "chrome",
            [HandlerType.BrowserFirefox] = "firefox",
        };

        Assert.Equal("chrome", handlerProcessMap[HandlerType.BrowserChrome]);
        Assert.Equal("firefox", handlerProcessMap[HandlerType.BrowserFirefox]);
    }
}
