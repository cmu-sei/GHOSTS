// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Domain;
using Newtonsoft.Json;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class SftpHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Sftp,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["Credentials"] = JsonConvert.SerializeObject(new { Credentials = new Dictionary<string, object> { ["test"] = new { Username = "user", Password = "pass" } } })
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object> { "192.168.1.1|test|get file.txt" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Sftp_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var sftpHandler = new Sftp(timeline, handler, cts.Token);

        Assert.NotNull(sftpHandler);
    }

    [Fact]
    public void Sftp_CanBeConstructed_WithNoCredentials()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var sftpHandler = new Sftp(timeline, handler, cts.Token);
        Assert.NotNull(sftpHandler);
    }

    [Fact]
    public async Task Sftp_Run_WithNoCredentials_CompletesWithoutException()
    {
        // When no credentials are supplied, the handler logs an error and returns
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var sftpHandler = new Sftp(timeline, handler, cts.Token);

        await sftpHandler.Run();
    }

    [Fact]
    public async Task Sftp_Run_WithCredentials_FailsOnConnection()
    {
        // When credentials are supplied but the host is unreachable,
        // the handler catches the connection exception and continues
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var sftpHandler = new Sftp(timeline, handler, cts.Token);

        await sftpHandler.Run();
    }
}
