// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;

namespace ghosts.api.Infrastructure.Services
{
    public interface IBackgroundQueue
    {
        void Enqueue(QueueEntry item);
        Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken);
        IEnumerable<QueueEntry> GetAll();
    }

    public class BackgroundQueue : IBackgroundQueue
    {
        private readonly ConcurrentQueue<QueueEntry> _items = new();
        private readonly SemaphoreSlim _semaphore = new(0);

        public void Enqueue(QueueEntry item)
        {
            ArgumentNullException.ThrowIfNull(item);

            _items.Enqueue(item);
            _semaphore.Release();
        }

        public async Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            _items.TryDequeue(out var item);

            return item;
        }

        public IEnumerable<QueueEntry> GetAll()
        {
            return _items;
        }
    }
}
