// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Models;

namespace Ghosts.Api.Services
{
    public interface IBackgroundQueue
    {
        void Enqueue(QueueEntry item);
        Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken);
        IEnumerable<QueueEntry> GetAll();
    }

    public class BackgroundQueue : IBackgroundQueue
    {
        private readonly ConcurrentQueue<QueueEntry> _items = new ConcurrentQueue<QueueEntry>();
        private readonly SemaphoreSlim _semaphone = new SemaphoreSlim(0);

        public void Enqueue(QueueEntry item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            this._items.Enqueue(item);
            this._semaphone.Release();
        }

        public async Task<QueueEntry> DequeueAsync(CancellationToken cancellationToken)
        {
            await _semaphone.WaitAsync(cancellationToken);
            _items.TryDequeue(out var item);

            return item;
        }

        public IEnumerable<QueueEntry> GetAll()
        {
            return this._items;
        }
    }
}