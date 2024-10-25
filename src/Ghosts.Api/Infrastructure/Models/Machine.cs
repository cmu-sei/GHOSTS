// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Ghosts.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ghosts.api.Infrastructure.Models
{
    public class FindMachineResponse
    {
        public Machine Machine { get; set; }
        public string Error { get; set; }

        public FindMachineResponse()
        {
            Machine = new Machine();
        }

        public bool IsValid()
        {
            return string.IsNullOrEmpty(Error);
        }
    }

    [Table("machines")]
    public class Machine
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum UpDownStatus
        {
            Unknown = 0,
            Up = 1,
            UpWithErrors = 2,
            Down = 9,
            DownWithErrors = 10
        }

        public Machine()
        {
            History = new List<MachineHistoryItem>();
            HistoryTimeline = new List<HistoryTimeline>();
            HistoryHealth = new List<HistoryHealth>();
            HistoryTrackables = new List<HistoryTrackable>();
            StatusUp = UpDownStatus.Unknown;
            LastReportedUtc = CreatedUtc;
            CreatedUtc = DateTime.UtcNow;
        }

        [Key] public Guid Id { get; set; }

        public string Name { get; set; }
        public string FQDN { get; set; }
        public string Domain { get; set; }
        public string Host { get; set; }
        public string ResolvedHost { get; set; }
        public string HostIp { get; set; }
        public string IPAddress { get; set; }
        public string CurrentUsername { get; set; }
        public string ClientVersion { get; set; }
        public IList<MachineHistoryItem> History { get; set; }
        public IList<HistoryHealth> HistoryHealth { get; set; }
        public IList<HistoryTimeline> HistoryTimeline { get; set; }

        public IList<HistoryTrackable> HistoryTrackables { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public StatusType Status { get; set; }

        public DateTime CreatedUtc { get; }

        [NotMapped]
        public string StatusMessage
        {
            get
            {
                if ((StatusUp == UpDownStatus.Up ||
                     StatusUp == UpDownStatus.UpWithErrors ||
                     StatusUp == UpDownStatus.Unknown)
                    && LastReportedUtc < DateTime.UtcNow.AddMinutes(-Program.ApplicationSettings.OfflineAfterMinutes))
                    StatusUp = UpDownStatus.Down;

                return $"{Status} & {StatusUp}";
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public UpDownStatus StatusUp { get; set; }

        public DateTime LastReportedUtc { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Name) &&
                   !string.IsNullOrEmpty(FQDN) &&
                   !string.IsNullOrEmpty(HostIp) &&
                   //!string.IsNullOrEmpty(this.IpAddress) &&
                   !string.IsNullOrEmpty(CurrentUsername);
        }

        [NotMapped]
        public bool HadId { get; set; }

        public void AddHistoryHealth(HistoryHealth model)
        {
            HistoryHealth.Add(model);
            CalculateUpDown();
        }

        public void AddHistoryHealth(IList<HistoryHealth> model)
        {
            HistoryHealth = model;
            CalculateUpDown();
        }

        public void AddHistoryTimeline(HistoryTimeline model)
        {
            HistoryTimeline.Add(model);
            CalculateUpDown();
        }

        public void AddHistoryTimeline(IList<HistoryTimeline> model)
        {
            HistoryTimeline = model;
            CalculateUpDown();
        }

        public void AddHistoryMachine(MachineHistoryItem model)
        {
            History.Add(model);
            CalculateUpDown();
        }

        public void AddHistoryMachine(IList<MachineHistoryItem> model)
        {
            History = model;
            CalculateUpDown();
        }

        public void CalculateUpDown()
        {
            HistoryTimeline = HistoryTimeline.OrderByDescending(o => o.CreatedUtc).ToList();
            History = History.OrderByDescending(o => o.CreatedUtc).ToList();
            HistoryHealth = HistoryHealth.OrderByDescending(o => o.CreatedUtc).ToList();

            LastReportedUtc = CreatedUtc;
            var hasErrors = false;
            var isUp = false;

            var list = HistoryHealth.Where(o =>
                    o.Errors.Length > 0 ||
                    o.Internet.HasValue && o.Internet.Value == false ||
                    o.Permissions.HasValue && o.Permissions.Value == false
                )
                .OrderByDescending(o => o.CreatedUtc).ToList();

            hasErrors = list.Count > 0;

            while (!isUp)
            {
                if (History.Count > 0)
                {
                    var h = History.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = h.CreatedUtc.AddMinutes(Program.ApplicationSettings.OfflineAfterMinutes) > DateTime.UtcNow;
                        LastReportedUtc = h.CreatedUtc;
                    }
                }

                if (HistoryHealth.Count > 0)
                {
                    var h = HistoryHealth.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = h.CreatedUtc.AddMinutes(Program.ApplicationSettings.OfflineAfterMinutes) > DateTime.UtcNow;
                        if (h.CreatedUtc > LastReportedUtc)
                            LastReportedUtc = h.CreatedUtc;
                    }
                }

                if (HistoryTimeline.Count > 0)
                {
                    var h = HistoryTimeline.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = h.CreatedUtc.AddMinutes(Program.ApplicationSettings.OfflineAfterMinutes) > DateTime.UtcNow;
                        if (h.CreatedUtc > LastReportedUtc)
                            LastReportedUtc = h.CreatedUtc;
                    }
                }

                break;
            }

            if (hasErrors)
                StatusUp = isUp ? UpDownStatus.UpWithErrors : UpDownStatus.DownWithErrors;
            else
                StatusUp = isUp ? UpDownStatus.Up : UpDownStatus.Down;
        }

        [Table("history_machine")]
        public class MachineHistoryItem
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public enum HistoryType
            {
                Created = 0,
                [Display(Name = "Requested ID")] RequestedId = 5,
                [Display(Name = "Resynched ID")] Resynched = 6,
                [Display(Name = "Requested Updates")] RequestedUpdates = 10,
                [Display(Name = "Sent New Timeline")] SentNewTimeline = 11,

                [Display(Name = "Posted Client Results")]
                PostedResults = 20
            }

            public MachineHistoryItem()
            {
                CreatedUtc = DateTime.UtcNow;
            }

            public int Id { get; set; }
            public DateTime CreatedUtc { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public HistoryType Type { get; set; }

            public string Object { get; set; }

            [ForeignKey("MachineId")] public virtual Guid MachineId { get; set; }
        }
    }

    [NotMapped]
    public class MachineListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
