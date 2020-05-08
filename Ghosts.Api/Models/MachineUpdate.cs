// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using Ghosts.Domain;

namespace Ghosts.Api.Models
{
    [Table("machineupdates")]
    public class MachineUpdate
    {
        public int Id { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }

        public UpdateClientConfig.UpdateType Type { get; set; }

        public DateTime ActiveUtc { get; set; }
        public DateTime CreatedUtc { get; set; }

        public StatusType Status { get; set; }

        public string Update { get; set; }
    }
}