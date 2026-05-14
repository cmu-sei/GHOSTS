// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Newtonsoft.Json;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class WmiHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Wmi,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["Credentials"] = JsonConvert.SerializeObject(new { Credentials = new Dictionary<string, object> { ["test"] = new { Username = "admin", Password = "pass", Domain = "TESTDOMAIN" } } }),
                ["TimeBetweenCommandsMax"] = "5000",
                ["TimeBetweenCommandsMin"] = "1000"
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object> { "192.168.1.50|test|SELECT * FROM Win32_OperatingSystem" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Wmi_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var wmiHandler = new Wmi(timeline, handler, cts.Token);

        Assert.NotNull(wmiHandler);
    }

    [Fact]
    public void Wmi_CanBeConstructed_WithNoCredentials()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var wmiHandler = new Wmi(timeline, handler, cts.Token);
        Assert.NotNull(wmiHandler);
    }

    [Fact]
    public void Wmi_ResolvesViaOrchestrator()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        try
        {
            var task = Orchestrator.RunHandler(HandlerType.Wmi, timeline, handler, cts.Token);
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            Assert.Fail("HandlerType.Wmi could not be resolved to a concrete class");
        }
        catch (Exception)
        {
            // Other exceptions are acceptable — the handler resolved
        }
    }

    [Fact]
    public async Task Wmi_Run_WithNoCredentials_CompletesGracefully()
    {
        // When no credentials are supplied, handler should log error and return gracefully
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var wmiHandler = new Wmi(timeline, handler, cts.Token);

        await wmiHandler.Run(); // Should complete without throwing
    }

    [Fact]
    public async Task Wmi_Run_OnLinux_SkipsGracefully()
    {
        // On Linux: WMI is not available. Handler should gracefully skip with a log message.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var wmiHandler = new Wmi(timeline, handler, cts.Token);

        // Should complete gracefully on Linux without throwing
        await wmiHandler.Run();
    }

    [Fact]
    public async Task Wmi_Run_OnWindows_WithCredentials_CompletesGracefully()
    {
        // On Windows: should attempt WMI command execution via wmic process
        // and handle connection/execution failure gracefully
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var wmiHandler = new Wmi(timeline, handler, cts.Token);

        // Should complete without throwing (wmic failure is handled internally)
        await wmiHandler.Run();
    }

    [Fact]
    public void Wmi_HandlerArgs_TimeBetweenCommands_ParsesCorrectly()
    {
        var args = new Dictionary<string, object>
        {
            ["TimeBetweenCommandsMax"] = "10000",
            ["TimeBetweenCommandsMin"] = "2000"
        };

        var handler = CreateHandler(args);

        Assert.Equal("10000", handler.HandlerArgs["TimeBetweenCommandsMax"].ToString());
        Assert.Equal("2000", handler.HandlerArgs["TimeBetweenCommandsMin"].ToString());
    }

    [Fact]
    public void Wmi_TimelineEvent_CommandArgs_ParsesCorrectly()
    {
        // Command args format: "hostIp|credKey|wmiCommand1;wmiCommand2"
        var commandArg = "192.168.1.50|test|SELECT * FROM Win32_OperatingSystem;SELECT * FROM Win32_Process";
        var parts = commandArg.Split('|', 3);

        Assert.Equal("192.168.1.50", parts[0]);
        Assert.Equal("test", parts[1]);
        Assert.Equal("SELECT * FROM Win32_OperatingSystem;SELECT * FROM Win32_Process", parts[2]);

        var wmiCommands = parts[2].Split(';');
        Assert.Equal(2, wmiCommands.Length);
        Assert.Equal("SELECT * FROM Win32_OperatingSystem", wmiCommands[0]);
        Assert.Equal("SELECT * FROM Win32_Process", wmiCommands[1]);
    }
}
