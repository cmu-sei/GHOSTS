// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Concurrent;

namespace Ghosts.Client.Lite.Infrastructure.Comms.ClientSocket;

public class BackgroundTaskQueue
{
    private ConcurrentQueue<QueueEntry> _workItems = new ();
    private SemaphoreSlim _signal = new SemaphoreSlim(0);

    public IEnumerable<QueueEntry> GetAll()
    {
        return _workItems;
    }
    
    public void Enqueue(QueueEntry workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }

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
