// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class OutlookHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Outlook,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "send", CommandArgs = new List<object>(), DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Outlook_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var outlookHandler = new Outlook(timeline, handler, cts.Token);

        Assert.NotNull(outlookHandler);
    }

    [Fact]
    public async Task Outlook_Run_WithNoSmtpHost_CompletesWithoutThrowing()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var outlookHandler = new Outlook(timeline, handler, cts.Token);

        // Without smtp-host, the handler logs an error and returns
        await outlookHandler.Run();
    }

    [Fact]
    public async Task Outlook_Run_WithSmtpHost_FailsOnConnectionGracefully()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["smtp-host"] = "192.0.2.1",
            ["smtp-port"] = "587",
            ["username"] = "test@example.com",
            ["password"] = "password123",
            ["delay-jitter"] = "0"
        });
        handler.TimeLineEvents = new List<TimelineEvent>
        {
            new()
            {
                Command = "send",
                CommandArgs = new List<object>
                {
                    "test@example.com",
                    "recipient@example.com",
                    "",
                    "",
                    "Test Subject",
                    "Test Body",
                    "PlainText",
                    ""
                },
                DelayAfter = 0,
                DelayBefore = 0
            }
        };
        var cts = new CancellationTokenSource();

        var outlookHandler = new Outlook(timeline, handler, cts.Token);

        // Connection will fail but handler should not crash
        var ex = await Record.ExceptionAsync(() => outlookHandler.Run());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Outlook_Run_RespectsCanc()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["smtp-host"] = "192.0.2.1",
            ["initial-outlook-delay"] = "60000"
        });
        var cts = new CancellationTokenSource();

        var outlookHandler = new Outlook(timeline, handler, cts.Token);

        // Cancel immediately
        cts.Cancel();
        var ex = await Record.ExceptionAsync(() => outlookHandler.Run());

        // Should get OperationCanceledException (wrapped or direct)
        Assert.True(ex == null || ex is OperationCanceledException);
    }
}
