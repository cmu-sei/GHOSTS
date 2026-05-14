// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class HandlerResolutionTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(HandlerType type)
    {
        return new TimelineHandler
        {
            HandlerType = type,
            Loop = false,
            HandlerArgs = new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "test", CommandArgs = new List<object>(), DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Theory]
    [InlineData(HandlerType.Ssh)]
    [InlineData(HandlerType.Sftp)]
    [InlineData(HandlerType.Ftp)]
    public async Task RunHandler_ResolvesNetworkHandlers_Successfully(HandlerType handlerType)
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(handlerType);
        var cts = new CancellationTokenSource();

        // These handlers resolve and complete (returning early due to missing credentials)
        var task = Orchestrator.RunHandler(handlerType, timeline, handler, cts.Token);
        Assert.NotNull(task);
        await task; // Should complete without throwing
    }

    /// <summary>
    /// Tests handler types that resolve safely without launching external processes.
    /// Excluded: Clicks/Rdp (X11 P/Invoke crashes headless), Reboot (calls systemctl),
    /// browsers/curl (launch processes).
    /// </summary>
    [Theory]
    [InlineData(HandlerType.Watcher)]
    [InlineData(HandlerType.Pidgin)]
    [InlineData(HandlerType.Wmi)]
    [InlineData(HandlerType.Outlookv2)]
    [InlineData(HandlerType.Notepad)]
    [InlineData(HandlerType.NpcSystem)]
    [InlineData(HandlerType.Aws)]
    [InlineData(HandlerType.Azure)]
    [InlineData(HandlerType.Word)]
    public void RunHandler_ResolvesHandlerTypes_ToConcreteClass(HandlerType handlerType)
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(handlerType);
        var cts = new CancellationTokenSource();

        // Verify the handler resolves (does not throw NotSupportedException)
        // It may throw other exceptions during execution, which is fine
        try
        {
            var task = Orchestrator.RunHandler(handlerType, timeline, handler, cts.Token);
            // If we get here, the handler was resolved and started
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            // This is what we're testing against - this should NOT happen
            Assert.Fail($"HandlerType {handlerType} could not be resolved to a concrete class");
        }
        catch (Exception)
        {
            // Other exceptions (NotImplementedException, InvalidOperationException, etc.)
            // are acceptable - they mean the handler was resolved but failed during execution
        }
    }

    /// <summary>
    /// Excel and PowerPoint handler classes are named ExcelHandler/PowerPointHandler
    /// rather than matching the enum name. This test documents that the current
    /// RunHandler resolution does not find them (known naming discrepancy).
    /// </summary>
    [Theory]
    [InlineData(HandlerType.Excel)]
    [InlineData(HandlerType.PowerPoint)]
    public void RunHandler_HandlerTypesWithNamingMismatch_ThrowNotSupported(HandlerType handlerType)
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(handlerType);
        var cts = new CancellationTokenSource();

        var ex = Record.Exception(() =>
        {
            _ = Orchestrator.RunHandler(handlerType, timeline, handler, cts.Token);
        });

        // These throw because the class name doesn't match the enum name
        // (e.g., ExcelHandler vs Excel)
        Assert.NotNull(ex);
        Assert.IsType<NotSupportedException>(ex);
    }

    [Fact]
    public void RunHandler_InvalidHandlerType_ThrowsNotSupportedException()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler((HandlerType)9999);
        var cts = new CancellationTokenSource();

        var ex = Record.Exception(() =>
        {
            _ = Orchestrator.RunHandler((HandlerType)9999, timeline, handler, cts.Token);
        });

        Assert.NotNull(ex);
        Assert.IsType<NotSupportedException>(ex);
    }
}
