// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;

namespace Ghosts.Domain.Models
{
    public class ThreadJob
    {
        public Guid TimelineId { get; set; }
        public Thread Thread { get; set; }
    }
}
