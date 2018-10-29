// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [Table("groups")]
    public class Group
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }

        public IList<GroupMachine> GroupMachines { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }

        [NotMapped]
        public IList<Machine> Machines { get; set; }

        public Group()
        {
            this.GroupMachines = new List<GroupMachine>();
            this.Machines = new List<Machine>();
        }
    }

    [Table("groupmachines")]
    public class GroupMachine
    {
        public int Id { get; set; }
        [ForeignKey("GroupId")]
        public int GroupId { get; set; }
        [ForeignKey("MachineId")]
        public Guid MachineId { get; set; }
    }
}
