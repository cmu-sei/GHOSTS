// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain.Models;
using Xunit;

namespace Ghosts.Client.Universal.Tests;

public class ShutdownTests
{
    [Fact]
    public async Task CancellationTokenSource_WhenCancelled_TaskObservesCancellation()
    {
        var cts = new CancellationTokenSource();
        var cancellationObserved = false;

        var task = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }
        });

        // Give the task time to start
        await Task.Delay(100);
        cts.Cancel();
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(cancellationObserved);
    }

    [Fact]
    public void RunningTasks_ConcurrentDictionary_TracksAddedTasks()
    {
        var runningTasks = new ConcurrentDictionary<Guid, TaskJob>();
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();

        var timelineId1 = Guid.NewGuid();
        var timelineId2 = Guid.NewGuid();

        var added1 = runningTasks.TryAdd(Guid.NewGuid(), new TaskJob
        {
            TimelineId = timelineId1,
            CancellationTokenSource = cts1,
            Task = Task.CompletedTask
        });

        var added2 = runningTasks.TryAdd(Guid.NewGuid(), new TaskJob
        {
            TimelineId = timelineId2,
            CancellationTokenSource = cts2,
            Task = Task.CompletedTask
        });

        Assert.True(added1);
        Assert.True(added2);
        Assert.Equal(2, runningTasks.Count);
    }

    [Fact]
    public void RunningTasks_CancelAll_AllTokensCancelled()
    {
        var runningTasks = new ConcurrentDictionary<Guid, TaskJob>();
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();

        runningTasks.TryAdd(Guid.NewGuid(), new TaskJob
        {
            TimelineId = Guid.NewGuid(),
            CancellationTokenSource = cts1,
            Task = Task.CompletedTask
        });

        runningTasks.TryAdd(Guid.NewGuid(), new TaskJob
        {
            TimelineId = Guid.NewGuid(),
            CancellationTokenSource = cts2,
            Task = Task.CompletedTask
        });

        // Simulate shutdown logic from Program.CurrentDomain_ProcessExit
        foreach (var job in runningTasks.Values)
        {
            if (job.CancellationTokenSource is { Token.CanBeCanceled: true })
                job.CancellationTokenSource.Cancel();
        }

        Assert.True(cts1.IsCancellationRequested);
        Assert.True(cts2.IsCancellationRequested);
    }
}
