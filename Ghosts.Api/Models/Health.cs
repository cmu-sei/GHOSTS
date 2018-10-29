// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Models
{
    [Table("historyhealth")]
    public class HistoryHealth
    {
        public int Id { get; set; }
        [ForeignKey("MachineId")]
        public Guid MachineId { get; set; }
        public DateTime CreatedUtc { get; set; }

        public bool? Internet { get; set; }
        public bool? Permissions { get; set; }
        public long ExecutionTime { get; set; }
        public string Errors { get; set; }
        public string LoggedOnUsers { get; set; }
        public string Stats { get; set; }

        public HistoryHealth()
        {
            this.CreatedUtc = DateTime.UtcNow;
        }
    }

    [Table("historytimeline")]
    public class HistoryTimeline
    {
        public int Id { get; set; }
        [ForeignKey("MachineId")]
        public Guid MachineId { get; set; }
        public DateTime CreatedUtc { get; set; }

        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string Result { get; set; }

        public HistoryTimeline()
        {
            this.CreatedUtc = DateTime.UtcNow;
        }
    }

    [Table("historytrackables")]
    public class HistoryTrackable
    {
        public int Id { get; set; }
        [ForeignKey("MachineId")]
        public Guid MachineId { get; set; }
        [ForeignKey("TrackableId")]
        public Guid TrackableId { get; set; }
        public DateTime CreatedUtc { get; set; }

        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string Result { get; set; }

        public HistoryTrackable()
        {
            this.CreatedUtc = DateTime.UtcNow;
        }
    }
}
