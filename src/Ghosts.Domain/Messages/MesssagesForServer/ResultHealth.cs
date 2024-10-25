// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Domain
{
    /// <summary>
    ///     the client results of running a health check
    /// </summary>
    public class ResultHealth
    {
        public ResultHealth()
        {
            Errors = new List<string>();
            LoggedOnUsers = new List<string>();
            Stats = new MachineStats();
        }

        public bool Internet { get; set; }
        public bool Permissions { get; set; }
        public long ExecutionTime { get; set; }

        public List<string> Errors { get; set; }
        public List<string> LoggedOnUsers { get; set; }
        public MachineStats Stats { get; set; }

        public class MachineStats
        {
            public float Memory { get; set; }
            public float Cpu { get; set; }
            public float DiskSpace { get; set; }
        }
    }
}
