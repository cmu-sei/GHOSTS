// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Domain.Messages.MesssagesForServer
{
    [Table("surveys")]
    public class Survey
    {
        public Survey()
        {
            Interfaces = new List<Interface>();
            LocalUsers = new List<LocalUser>();
            Drives = new List<DriveInfo>();
            Processes = new List<LocalProcess>();
            EventLogs = new List<EventLog>();
            Ports = new List<Port>();
        }

        [Key] public int Id { get; set; }

        public Guid MachineId { get; set; }
        public DateTime Created { get; set; }

        public TimeSpan Uptime { get; set; }

        public IList<Interface> Interfaces { get; set; }
        public IList<LocalUser> LocalUsers { get; set; }
        public IList<DriveInfo> Drives { get; set; }
        public IList<LocalProcess> Processes { get; set; }
        public IList<EventLog> EventLogs { get; set; }
        public IList<Port> Ports { get; set; }

        [Table("survey_interfaces")]
        public class Interface
        {
            public Interface()
            {
                Bindings = new List<InterfaceBinding>();
            }

            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string Name { get; set; }
            public IList<InterfaceBinding> Bindings { get; set; }

            [Table("survey_interface_bindings")]
            public class InterfaceBinding
            {
                [Key] public int Id { get; set; }

                [ForeignKey("InterfaceId")] public int InterfaceId { get; set; }

                public string InternetAddress { get; set; }
                public string PhysicalAddress { get; set; }
                public string Type { get; set; }
            }
        }

        [Table("survey_event_logs")]
        public class EventLog
        {
            public EventLog()
            {
                Entries = new List<EventLogEntry>();
            }

            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string Name { get; set; }
            public IList<EventLogEntry> Entries { get; set; }

            [Table("survey_event_log_entries")]
            public class EventLogEntry
            {
                [Key] public int Id { get; set; }

                [ForeignKey("EventLogId")] public int EventLogId { get; set; }

                public DateTime Created { get; set; }
                public string EntryType { get; set; }
                public string Source { get; set; }
                public string Message { get; set; }
            }
        }

        [Table("survey_users")]
        public class LocalUser
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string Username { get; set; }
            public string Domain { get; set; }
        }

        [Table("survey_local_processes")]
        public class LocalProcess
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public long PrivateMemorySize64 { get; set; } = 0;
            public string MainWindowTitle { get; set; }
            public string ProcessName { get; set; }
            public DateTime? StartTime { get; set; }
            public string FileName { get; set; }
            public string Owner { get; set; }
            public string OwnerDomain { get; set; }
            public string OwnerSid { get; set; }
        }

        [Table("survey_drives")]
        public class DriveInfo
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public long AvailableFreeSpace { get; set; }
            public string DriveFormat { get; set; }
            public string DriveType { get; set; }
            public bool IsReady { get; set; }
            public string Name { get; set; }
            public string RootDirectory { get; set; }
            public long TotalFreeSpace { get; set; }
            public long TotalSize { get; set; }
            public string VolumeLabel { get; set; }
        }

        [Table("survey_ports")]
        public class Port
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string LocalPort { get; set; }
            public string LocalAddress { get; set; }
            public string ForeignAddress { get; set; }
            public string ForeignPort { get; set; }
            public int PID { get; set; }
            public string Process { get; set; }
            public string Protocol { get; set; }
            public string State { get; set; }
        }
    }
}
