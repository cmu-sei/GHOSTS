// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ghosts.api.Infrastructure.Models
{
    [Table("groups")]
    public class Group
    {
        public Group()
        {
            GroupMachines = new List<GroupMachine>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public string Name { get; set; }

        public IList<GroupMachine> GroupMachines { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }
    }

    [Table("group_machines")]
    public class GroupMachine
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("GroupId")] public int GroupId { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }
    }
}
