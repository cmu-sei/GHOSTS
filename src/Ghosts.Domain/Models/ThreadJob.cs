// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Domain.Models
{
    public class TaskJob
    {
        public Guid TimelineId { get; set; }

        /// <summary>
        /// for the classic windows app
        /// </summary>
        public Thread Thread { get; set; }

        /// <summary>
        /// for the modern universal app
        /// </summary>
        public Task Task { get; set; }

        /// <summary>
        /// for the modern universal app
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; set; }
    }
}
