// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Ghosts.Api.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Models
{
    [Table("machines")]
    public class Machine
    {
        [Key]
        public Guid Id { get; set; }
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
        public DateTime CreatedUtc { get; private set; }

        [NotMapped]
        public string StatusMessage
        {
            get
            {
                if ((this.StatusUp == UpDownStatus.Up ||
                    this.StatusUp == UpDownStatus.UpWithErrors ||
                    this.StatusUp == UpDownStatus.Unknown)
                    && this.LastReportedUtc < DateTime.UtcNow.AddMinutes(-Program.ClientConfig.OfflineAfterMinutes))
                    this.StatusUp = UpDownStatus.Down;

                return $"{this.Status} & {this.StatusUp}";
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public UpDownStatus StatusUp { get; set; }
        public DateTime LastReportedUtc { get; set; }

        public Machine()
        {
            this.History = new List<MachineHistoryItem>();
            this.HistoryTimeline = new List<HistoryTimeline>();
            this.HistoryHealth = new List<HistoryHealth>();
            this.HistoryTrackables = new List<HistoryTrackable>();
            this.StatusUp = UpDownStatus.Unknown;
            this.LastReportedUtc = this.CreatedUtc;
            this.CreatedUtc = DateTime.UtcNow;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(this.Name) &&
                   !string.IsNullOrEmpty(this.FQDN) &&
                   !string.IsNullOrEmpty(this.HostIp) &&
                   //!string.IsNullOrEmpty(this.IpAddress) &&
                   !string.IsNullOrEmpty(this.CurrentUsername);
        }

        public void AddHistoryHealth(HistoryHealth model)
        {
            this.HistoryHealth.Add(model);
            this.CalculateUpDown();
        }

        public void AddHistoryHealth(IList<HistoryHealth> model)
        {
            this.HistoryHealth = model;
            this.CalculateUpDown();
        }

        public void AddHistoryTimeline(HistoryTimeline model)
        {
            this.HistoryTimeline.Add(model);
            this.CalculateUpDown();
        }

        public void AddHistoryTimeline(IList<HistoryTimeline> model)
        {
            this.HistoryTimeline = model;
            this.CalculateUpDown();
        }

        public void AddHistoryMachine(MachineHistoryItem model)
        {
            this.History.Add(model);
            this.CalculateUpDown();
        }

        public void AddHistoryMachine(IList<MachineHistoryItem> model)
        {
            this.History = model;
            this.CalculateUpDown();
        }

        public void CalculateUpDown()
        {
            this.HistoryTimeline = this.HistoryTimeline.OrderByDescending(o => o.CreatedUtc).ToList();
            this.History = this.History.OrderByDescending(o => o.CreatedUtc).ToList();
            this.HistoryHealth = this.HistoryHealth.OrderByDescending(o => o.CreatedUtc).ToList();
            
            this.LastReportedUtc = this.CreatedUtc;
            var hasErrors = false;
            var isUp = false;

            var list = this.HistoryHealth.Where(o =>
                    (o.Errors.Length > 0 ||
                     (o.Internet.HasValue && o.Internet.Value == false) ||
                     (o.Permissions.HasValue && o.Permissions.Value == false)
                    )
                )
                .OrderByDescending(o => o.CreatedUtc).ToList();

            hasErrors = list.Count > 0;

            while (!isUp)
            {
                if (this.History.Count > 0)
                {
                    var h = this.History.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = (h.CreatedUtc.AddMinutes(Program.ClientConfig.OfflineAfterMinutes) > DateTime.UtcNow);
                        this.LastReportedUtc = h.CreatedUtc;
                    }
                }

                if (this.HistoryHealth.Count > 0)
                {
                    var h = this.HistoryHealth.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = (h.CreatedUtc.AddMinutes(Program.ClientConfig.OfflineAfterMinutes) > DateTime.UtcNow);
                        if (h.CreatedUtc > this.LastReportedUtc)
                            this.LastReportedUtc = h.CreatedUtc;
                    }
                }

                if (this.HistoryTimeline.Count > 0)
                {
                    var h = this.HistoryTimeline.OrderBy(o => o.CreatedUtc).Last();
                    if (h != null)
                    {
                        isUp = (h.CreatedUtc.AddMinutes(Program.ClientConfig.OfflineAfterMinutes) > DateTime.UtcNow);
                        if (h.CreatedUtc > this.LastReportedUtc)
                            this.LastReportedUtc = h.CreatedUtc;
                    }
                }
                
                break;
            }

            if (hasErrors)
            {
                this.StatusUp = isUp ? UpDownStatus.UpWithErrors : UpDownStatus.DownWithErrors;
            }
            else
            {
                this.StatusUp = isUp ? UpDownStatus.Up : UpDownStatus.Down;
            }
        }

        [Table("historymachine")]
        public class MachineHistoryItem
        {
            public int Id { get; set; }
            public DateTime CreatedUtc { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public HistoryType Type { get; set; }
            public string Object { get; set; }

            [ForeignKey("MachineId")]
            public virtual Guid MachineId { get; set; }

            public MachineHistoryItem()
            {
                this.CreatedUtc = DateTime.UtcNow;
            }

            public enum HistoryType
            {
                Created = 0,
                [Display(Name = "Requested ID")]
                RequestedId = 5,
                [Display(Name = "Resynched ID")]
                Resynched = 6,
                [Display(Name = "Requested Updates")]
                RequestedUpdates = 10,
                [Display(Name = "Sent New Timeline")]
                SentNewTimeline = 11,
                [Display(Name = "Posted Client Results")]
                PostedResults = 20
            }
        }

        public enum UpDownStatus
        {
            Unknown = 0,
            Up = 1,
            UpWithErrors = 2,
            Down = 9,
            DownWithErrors = 10
        }
    }

    [NotMapped]
    public class MachineListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
