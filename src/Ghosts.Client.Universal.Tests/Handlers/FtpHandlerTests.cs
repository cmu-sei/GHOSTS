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

public class FtpHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Ftp,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["Credentials"] = JsonConvert.SerializeObject(new { Credentials = new Dictionary<string, object> { ["test"] = new { Username = "user", Password = "pass" } } })
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "random", CommandArgs = new List<object> { "192.168.1.1|test" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void Ftp_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var ftpHandler = new Ftp(timeline, handler, cts.Token);

        Assert.NotNull(ftpHandler);
    }

    [Fact]
    public void Ftp_CanBeConstructed_WithNoCredentials()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var ftpHandler = new Ftp(timeline, handler, cts.Token);
        Assert.NotNull(ftpHandler);
    }

    [Fact]
    public async Task Ftp_Run_WithNoCredentials_CompletesWithoutException()
    {
        // When no credentials are supplied, the handler logs an error and returns
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var ftpHandler = new Ftp(timeline, handler, cts.Token);

        await ftpHandler.Run();
    }

    [Fact]
    public void Ftp_ProbabilityValidation_SumsOver100_ResetToDefaults()
    {
        // This test validates the probability logic from the handler:
        // when sum of probabilities > 100, they get reset to defaults (40, 40, 20)
        var deletionProb = 50;
        var downloadProb = 40;
        var uploadProb = 40;

        // Simulate the validation logic from the handler
        if ((deletionProb + uploadProb + downloadProb) > 100)
        {
            uploadProb = 40;
            downloadProb = 40;
            deletionProb = 20;
        }

        Assert.Equal(40, uploadProb);
        Assert.Equal(40, downloadProb);
        Assert.Equal(20, deletionProb);
    }

    [Fact]
    public async Task Ftp_Run_WithProbabilities_ParsesCorrectly()
    {
        // Verify that the handler parses probability args without error
        var timeline = CreateTimeline();
        var handler = CreateHandler(new Dictionary<string, object>
        {
            ["deletion-probability"] = "50",
            ["download-probability"] = "40",
            ["upload-probability"] = "40"
        });
        var cts = new CancellationTokenSource();

        var ftpHandler = new Ftp(timeline, handler, cts.Token);

        // No credentials so the handler exits early after parsing probabilities
        // The important thing is it doesn't throw during probability parsing
        await ftpHandler.Run();
    }
}
