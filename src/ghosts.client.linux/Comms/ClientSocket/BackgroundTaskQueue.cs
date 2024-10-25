// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ghosts.client.linux.Comms.ClientSocket;

public class BackgroundTaskQueue
{
    private readonly ConcurrentQueue<QueueEntry> _workItems = new();
    private readonly SemaphoreSlim _signal = new(0);

    public IEnumerable<QueueEntry> GetAll()
    {
        return _workItems;
    }

    public void Enqueue(QueueEntry workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        _workItems.Enqueue(workItem);
        _signal.Release();
    }

    public async Task<QueueEntry> DequeueAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        _workItems.TryDequeue(out var item);
        return item;
    }
}
