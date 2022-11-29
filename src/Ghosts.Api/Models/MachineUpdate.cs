// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using Ghosts.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [Table("machine_updates")]
    public class MachineUpdate
    {
        public int Id { get; set; }

        public Guid MachineId { get; set; }

        public string Username { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public UpdateClientConfig.UpdateType Type { get; set; }

        public DateTime ActiveUtc { get; set; }
        public DateTime CreatedUtc { get; set; }

        public StatusType Status { get; set; }

        public string Update { get; set; }
    }
}