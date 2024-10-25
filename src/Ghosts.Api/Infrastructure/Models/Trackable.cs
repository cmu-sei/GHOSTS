// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ghosts.api.Infrastructure.Models
{
    [Table("trackables")]
    public class Trackable
    {
        [Key] public Guid Id { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }

        public string Name { get; set; }
    }
}
