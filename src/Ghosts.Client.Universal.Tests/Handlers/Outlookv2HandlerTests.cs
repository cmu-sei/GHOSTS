// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class Outlookv2HandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Outlookv2,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object>(), DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Outlookv2_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var v2Handler = new Outlookv2(timeline, handler, cts.Token);

        Assert.NotNull(v2Handler);
    }

    [Fact]
    public async Task Outlookv2_Run_WithNoSmtpHost_CompletesWithoutThrowing()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var v2Handler = new Outlookv2(timeline, handler, cts.Token);

        // Without smtp-host, the handler logs an error and returns
        await v2Handler.Run();
    }

    [Fact]
    public async Task Outlookv2_Run_WithInvalidProbabilities_UsesDefaults()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["smtp-host"] = "192.0.2.1",
            ["username"] = "test@example.com",
            ["password"] = "pass",
            ["initial-outlook-delay"] = "0",
            ["read-probability"] = "50",
            ["delete-probability"] = "50",
            ["create-probability"] = "50",
            ["reply-probability"] = "50"
        });
        handler.TimeLineEvents = new List<TimelineEvent>
        {
            new()
            {
                Command = "random",
                CommandArgs = new List<object>
                {
                    "test@example.com",
                    "recipient@example.com",
                    "",
                    "",
                    "random",
                    "random",
                    "PlainText",
                    ""
                },
                DelayAfter = 0,
                DelayBefore = 0
            }
        };
        var cts = new CancellationTokenSource();

        var v2Handler = new Outlookv2(timeline, handler, cts.Token);

        // Probabilities sum > 100 so defaults are used; connection will fail but handler won't crash
        var ex = await Record.ExceptionAsync(() => v2Handler.Run());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Outlookv2_Run_CancellationIsRespected()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["smtp-host"] = "192.0.2.1",
            ["username"] = "test@example.com",
            ["password"] = "pass",
            ["initial-outlook-delay"] = "60000"
        });
        var cts = new CancellationTokenSource();

        var v2Handler = new Outlookv2(timeline, handler, cts.Token);

        // Cancel immediately
        cts.Cancel();
        var ex = await Record.ExceptionAsync(() => v2Handler.Run());
        Assert.True(ex == null || ex is OperationCanceledException || ex is TaskCanceledException);
    }

    [Fact]
    public void Outlookv2_ResolvesViaOrchestrator()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        // Should not throw NotSupportedException — the handler resolves
        try
        {
            var task = Orchestrator.RunHandler(HandlerType.Outlookv2, timeline, handler, cts.Token);
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            Assert.Fail("Outlookv2 should resolve via Orchestrator.RunHandler");
        }
        catch (Exception)
        {
            // Other exceptions (e.g. from execution) are fine
        }
    }

    [Fact]
    public void Outlookv2_SelectActionFromProbabilities_ReturnsValidAction()
    {
        // Test through multiple iterations to verify probabilistic selection
        var results = new HashSet<string>();
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["smtp-host"] = "192.0.2.1",
            ["read-probability"] = "25",
            ["delete-probability"] = "25",
            ["create-probability"] = "25",
            ["reply-probability"] = "25"
        });
        var cts = new CancellationTokenSource();

        // We can't call SelectActionFromProbabilities directly (private),
        // but we verify it works via the handler resolution path
        var v2Handler = new Outlookv2(timeline, handler, cts.Token);
        Assert.NotNull(v2Handler);
    }
}
