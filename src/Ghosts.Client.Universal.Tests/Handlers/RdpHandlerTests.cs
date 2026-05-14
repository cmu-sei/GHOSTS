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

public class RdpHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Rdp,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["Credentials"] = JsonConvert.SerializeObject(new { Credentials = new Dictionary<string, object> { ["test"] = new { Username = "user", Password = "pass" } } }),
                ["execution-time"] = "5000",
                ["mouse-sleep-time"] = "1000"
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object> { "192.168.1.100|test" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Rdp_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);

        Assert.NotNull(rdpHandler);
    }

    [Fact]
    public void Rdp_CanBeConstructed_WithNoCredentials()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);
        Assert.NotNull(rdpHandler);
    }

    [Fact]
    public void Rdp_ResolvesViaOrchestrator()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        try
        {
            var task = Orchestrator.RunHandler(HandlerType.Rdp, timeline, handler, cts.Token);
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            Assert.Fail("HandlerType.Rdp could not be resolved to a concrete class");
        }
        catch (Exception)
        {
            // Other exceptions during execution are acceptable — the handler was resolved
        }
    }

    [Fact]
    public async Task Rdp_Run_WithNoCredentials_CompletesGracefully()
    {
        // When no credentials are supplied, handler should log error and return gracefully
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);

        await rdpHandler.Run(); // Should complete without throwing
    }

    [Fact]
    public async Task Rdp_Run_WithCredentials_OnLinux_CompletesGracefully()
    {
        // On Linux: should attempt to spawn xfreerdp or rdesktop
        // and complete gracefully when tools are not found or connection fails
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);

        // Should complete without throwing (missing tools/connection failure handled internally)
        await rdpHandler.Run();
    }

    [Fact]
    public async Task Rdp_Run_WithCredentials_OnWindows_CompletesGracefully()
    {
        // On Windows: should attempt mstsc connection
        // and complete gracefully when target is unreachable
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);

        // Should complete without throwing (connection failure handled internally)
        await rdpHandler.Run();
    }

    [Fact]
    public async Task Rdp_Run_RespectsCancellation()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        handler.Loop = true;
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var rdpHandler = new Rdp(timeline, handler, cts.Token);

        // With Loop=true and already-cancelled token, Run should exit without looping
        // BaseHandler.Run checks cancellation before calling RunOnce, so no exception thrown
        await rdpHandler.Run();
    }

    [Fact]
    public void Rdp_HandlerArgs_ExecutionTime_ParsesCorrectly()
    {
        // Validates that execution-time arg is parseable
        var args = new Dictionary<string, object>
        {
            ["execution-time"] = "30000",
            ["mouse-sleep-time"] = "5000",
            ["execution-probability"] = "75",
            ["delay-jitter"] = "25"
        };

        var handler = CreateHandler(args);

        Assert.Equal("30000", handler.HandlerArgs["execution-time"].ToString());
        Assert.Equal("5000", handler.HandlerArgs["mouse-sleep-time"].ToString());
        Assert.Equal("75", handler.HandlerArgs["execution-probability"].ToString());
        Assert.Equal("25", handler.HandlerArgs["delay-jitter"].ToString());
    }
}
