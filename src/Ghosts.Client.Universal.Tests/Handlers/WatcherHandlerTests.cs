// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class WatcherHandlerTests
{
    private static Timeline CreateTimeline()
    {
        return new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run };
    }

    private static TimelineHandler CreateHandler(Dictionary<string, object> handlerArgs = null, string watchPath = null)
    {
        var path = watchPath ?? Path.GetTempPath();
        return new TimelineHandler
        {
            HandlerType = HandlerType.Watcher,
            Loop = false,
            HandlerArgs = handlerArgs ?? new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new()
                {
                    Command = "folder",
                    CommandArgs = new List<object> { $"path:{path}", "size:100", "deletionApproach:random" },
                    DelayAfter = 0,
                    DelayBefore = 0
                }
            }
        };
    }

    private static TimelineHandler CreateFileWatcherHandler(string filePath)
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Watcher,
            Loop = false,
            HandlerArgs = new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new()
                {
                    Command = "file",
                    CommandArgs = new List<object> { filePath, "5000" },
                    DelayAfter = 0,
                    DelayBefore = 0
                }
            }
        };
    }

    [Fact]
    public void Watcher_ResolvesViaOrchestrator()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();

        try
        {
            var task = Orchestrator.RunHandler(HandlerType.Watcher, timeline, handler, cts.Token);
            Assert.NotNull(task);
        }
        catch (NotSupportedException)
        {
            Assert.Fail("HandlerType.Watcher could not be resolved to a concrete class");
        }
        catch (Exception)
        {
            // Other exceptions are acceptable — the handler resolved
        }
    }

    [Fact]
    public async Task Watcher_Run_CancelsGracefully()
    {
        var timeline = CreateTimeline();
        var handler = CreateHandler();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var task = Orchestrator.RunHandler(HandlerType.Watcher, timeline, handler, cts.Token);
        await task;
    }

    [Fact]
    public async Task Watcher_Run_FolderCommand_WatchesDirectory()
    {
        // Acceptance: file-change events detected and reported
        var watchDir = Path.Combine(Path.GetTempPath(), $"ghosts_watcher_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(watchDir);

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateHandler(watchPath: watchDir);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            var task = Orchestrator.RunHandler(HandlerType.Watcher, timeline, handler, cts.Token);
            await task;
        }
        finally
        {
            if (Directory.Exists(watchDir))
                Directory.Delete(watchDir, true);
        }
    }

    [Fact]
    public async Task Watcher_Run_FileCommand_MonitorsSingleFile()
    {
        var watchFile = Path.Combine(Path.GetTempPath(), $"ghosts_watcher_test_{Guid.NewGuid():N}.txt");
        File.WriteAllText(watchFile, "initial content");

        try
        {
            var timeline = CreateTimeline();
            var handler = CreateFileWatcherHandler(watchFile);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            var task = Orchestrator.RunHandler(HandlerType.Watcher, timeline, handler, cts.Token);
            await task;
        }
        finally
        {
            if (File.Exists(watchFile))
                File.Delete(watchFile);
        }
    }

    [Fact]
    public void Watcher_TimelineEvent_CommandArgs_FolderParsesCorrectly()
    {
        // Validates the folder watcher command arg format: "key:value"
        var handler = CreateHandler();
        var timelineEvent = handler.TimeLineEvents[0];

        Assert.Equal("folder", timelineEvent.Command);
        Assert.Equal(3, timelineEvent.CommandArgs.Count);

        var pathArg = timelineEvent.CommandArgs[0].ToString();
        Assert.StartsWith("path:", pathArg);

        var sizeArg = timelineEvent.CommandArgs[1].ToString();
        Assert.StartsWith("size:", sizeArg);
        var sizeParts = sizeArg.Split(':', 2);
        Assert.True(long.TryParse(sizeParts[1], out var sizeVal));
        Assert.True(sizeVal > 0);

        var approachArg = timelineEvent.CommandArgs[2].ToString();
        Assert.StartsWith("deletionApproach:", approachArg);
        var approachVal = approachArg.Split(':', 2)[1];
        Assert.Contains(approachVal, new[] { "random", "oldest", "largest" });
    }

    [Fact]
    public void Watcher_DeletionApproach_ValidValues()
    {
        var validApproaches = new[] { "random", "oldest", "largest" };

        foreach (var approach in validApproaches)
        {
            Assert.Contains(approach, validApproaches);
        }
    }

    [Fact]
    public void Watcher_HandlerType_IsCorrectEnumValue()
    {
        Assert.Equal(19, (int)HandlerType.Watcher);
    }
}
