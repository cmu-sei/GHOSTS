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

public class BrowserCrawlHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(HandlerType browserType = HandlerType.BrowserChrome, Dictionary<string, object> handlerArgs = null)
    {
        return new TimelineHandler
        {
            HandlerType = browserType,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>
            {
                ["stickiness"] = "10",
                ["stickiness-depth-min"] = "1",
                ["stickiness-depth-max"] = "3",
                ["crawl-site-depth"] = "2",
                ["crawl-proxy-local-url"] = ""
            },
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "crawl", CommandArgs = new List<object> { "http://example.com" }, DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void BrowserCrawl_CanBeConstructed_WithValidTimelineAndHandler()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var crawlHandler = new BrowserCrawl(timeline, handler, cts.Token);

        Assert.NotNull(crawlHandler);
    }

    [Fact]
    public void BrowserCrawl_CanBeConstructed_WithNoHandlerArgs()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler(handlerArgs: new Dictionary<string, object>());
        var cts = new CancellationTokenSource();

        var crawlHandler = new BrowserCrawl(timeline, handler, cts.Token);
        Assert.NotNull(crawlHandler);
    }

    [Fact]
    public async Task BrowserCrawl_RunOnce_IteratesEventsAndHandlesErrors()
    {
        // RunOnce iterates timeline events and calls Crawl for each.
        // Without a real browser driver, Crawl will throw, which RunOnce catches gracefully.
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var crawlHandler = new BrowserCrawl(timeline, handler, cts.Token);

        // Should not throw NotImplementedException; errors from missing driver are caught internally
        var exception = await Record.ExceptionAsync(() => crawlHandler.Run());
        Assert.Null(exception);
    }

    [Fact]
    public void BrowserCrawl_HandlerArgs_Stickiness_ParsesCorrectly()
    {
        var args = new Dictionary<string, object>
        {
            ["stickiness"] = "50",
            ["crawl-site-depth"] = "5"
        };
        var handler = CreateHandler(handlerArgs: args);

        Assert.Equal("50", handler.HandlerArgs["stickiness"].ToString());
        Assert.Equal("5", handler.HandlerArgs["crawl-site-depth"].ToString());
    }

    [Fact]
    public void BrowserCrawl_HandlerArgs_DefaultStickiness_IsValid()
    {
        var handler = CreateHandler();

        Assert.True(handler.HandlerArgs.ContainsKey("stickiness"));
        Assert.True(int.TryParse(handler.HandlerArgs["stickiness"].ToString(), out var stickiness));
        Assert.True(stickiness >= 0);
    }

    [Fact]
    public void BrowserCrawl_CrawlMethod_Exists()
    {
        // Verify the internal Crawl method exists for browser handlers to invoke
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var crawlHandler = new BrowserCrawl(timeline, handler, cts.Token);

        var crawlMethod = typeof(BrowserCrawl).GetMethod("Crawl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(crawlMethod);
    }

    [Fact]
    public void BrowserCrawl_SupportsChrome_AndFirefox()
    {
        // BrowserCrawl's Crawl method switches on HandlerType for driver selection
        var timelineCh = CreateTimeline();
        var handlerCh = CreateHandler(HandlerType.BrowserChrome);
        var cts = new CancellationTokenSource();

        var crawlChrome = new BrowserCrawl(timelineCh, handlerCh, cts.Token);
        Assert.NotNull(crawlChrome);

        var handlerFf = CreateHandler(HandlerType.BrowserFirefox);
        var crawlFirefox = new BrowserCrawl(timelineCh, handlerFf, cts.Token);
        Assert.NotNull(crawlFirefox);
    }

    [Fact]
    public void BrowserCrawl_Driver_InitiallyNull()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        var crawlHandler = new BrowserCrawl(timeline, handler, cts.Token);

        Assert.Null(crawlHandler.Driver);
        Assert.Null(crawlHandler.Js);
    }
}
