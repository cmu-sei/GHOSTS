// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Newtonsoft.Json;

namespace Ghosts.Domain
{
    /// <summary>
    /// Server passes this object to client as new timeline|health configuration - 
    /// when client saves this, client restarts and begins to use new config
    /// </summary>
    public class UpdateClientConfig
    {
        public enum UpdateType
        {
            Timeline = 0,
            Health = 1
        }

        public UpdateType Type { get; set; }
        public object Update { get; set; }

        [JsonIgnore]
        public Guid Key { get; set; }
    }

    public class TransferLogDump
    {
        public string Log { get; set; }
    }
}
