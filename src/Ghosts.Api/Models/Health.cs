// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Models
{
    [Table("history_health")]
    public class HistoryHealth
    {
        public HistoryHealth()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }

        public DateTime CreatedUtc { get; set; }

        public bool? Internet { get; set; }
        public bool? Permissions { get; set; }
        public long ExecutionTime { get; set; }
        public string Errors { get; set; }
        public string LoggedOnUsers { get; set; }
        public string Stats { get; set; }
    }

    [Table("history_timeline")]
    public class HistoryTimeline
    {
        public HistoryTimeline()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }

        public DateTime CreatedUtc { get; set; }

        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string Result { get; set; }
    }

    [Table("history_trackables")]
    public class HistoryTrackable
    {
        public HistoryTrackable()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }

        [ForeignKey("MachineId")] public Guid MachineId { get; set; }

        [ForeignKey("TrackableId")] public Guid TrackableId { get; set; }

        public DateTime CreatedUtc { get; set; }

        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string Result { get; set; }
    }
}