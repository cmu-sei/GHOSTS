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

public class SshHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Ssh,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["Credentials"] = JsonConvert.SerializeObject(new { Credentials = new Dictionary<string, object> { ["test"] = new { Username = "user", Password = "pass" } } })
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object> { "192.168.1.1|test|ls -la" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Ssh_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var sshHandler = new Ssh(timeline, handler, cts.Token);

        Assert.NotNull(sshHandler);
    }

    [Fact]
    public void Ssh_CanBeConstructed_WithNoCredentials()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var sshHandler = new Ssh(timeline, handler, cts.Token);
        Assert.NotNull(sshHandler);
    }

    [Fact]
    public async Task Ssh_Run_WithNoCredentials_CompletesWithoutException()
    {
        // When no credentials are supplied, the handler logs an error and returns
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var sshHandler = new Ssh(timeline, handler, cts.Token);

        // Should complete without throwing - the handler logs the missing credentials error
        await sshHandler.Run();
    }

    [Fact]
    public async Task Ssh_Run_WithCredentials_FailsOnConnection()
    {
        // When credentials are supplied but the host is unreachable,
        // the handler catches the connection exception and continues
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var sshHandler = new Ssh(timeline, handler, cts.Token);

        // Should complete - the handler catches connection failures internally
        await sshHandler.Run();
    }
}
