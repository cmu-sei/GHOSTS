// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ghosts.api.Infrastructure.Models
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

        public string Tags { get; set; }

        public IEnumerable<string> GetTags()
        {
            if (string.IsNullOrEmpty(Tags)) return new List<string>();
            return Tags.ToLower().Split(",");
        }

        public void SetTags(string value)
        {
            if (value != null) Tags = string.Join(",", value.ToLower());
        }
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
