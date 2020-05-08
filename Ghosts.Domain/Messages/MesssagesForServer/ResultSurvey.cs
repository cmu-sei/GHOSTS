// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Domain.Messages.MesssagesForServer
{
    [Table("Surveys")]
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

        [Table("SurveyInterfaces")]
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

            [Table("SurveyInterfaceBindings")]
            public class InterfaceBinding
            {
                [Key] public int Id { get; set; }

                [ForeignKey("InterfaceId")] public int InterfaceId { get; set; }

                public string InternetAddress { get; set; }
                public string PhysicalAddress { get; set; }
                public string Type { get; set; }
            }
        }

        [Table("SurveyEventLogs")]
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

            [Table("SurveyEventLogEntries")]
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

        [Table("SurveyUsers")]
        public class LocalUser
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string Username { get; set; }
            public string Domain { get; set; }
        }

        [Table("SurveyLocalProcesses")]
        public class LocalProcess
        {
            [Key] public int Id { get; set; }

            [ForeignKey("SurveyId")] public int SurveyId { get; set; }

            public string MainWindowTitle { get; set; }
            public string ProcessName { get; set; }
            public DateTime? StartTime { get; set; }
            public string FileName { get; set; }
            public string Owner { get; set; }
            public string OwnerDomain { get; set; }
            public string OwnerSid { get; set; }
        }

        [Table("SurveyDrives")]
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

        [Table("SurveyPorts")]
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

    /*
    public class ResultSurvey
    {
        public Guid MachineId { get; set; }
        public DateTime Created { get; set; }

        public TimeSpan Uptime { get; set; }

        public IList<Interface> Interfaces { get; set; }
        public IList<LocalUser> LocalUsers { get; set; }
        public IList<DriveInfo> Drives { get; set; }
        public IList<LocalProcess> Processes { get; set; }
        public IList<EventLog> EventLogs { get; set; }
        public IList<Port> Ports { get; set; }

        public ResultSurvey()
        {
            this.Drives = new List<DriveInfo>();
            this.EventLogs = new List<EventLog>();
            this.Interfaces = new List<Interface>();
            this.LocalUsers = new List<LocalUser>();
            this.Ports = new List<Port>();
            this.Processes = new List<LocalProcess>();
        }
        
        public class Interface
        {
            public string Name { get; set; }
            public IList<InterfaceBinding> Bindings { get; set; }

            public Interface()
            {
                this.Bindings = new List<InterfaceBinding>();
            }

            public class InterfaceBinding
            {
                public string InternetAddress { get; set; }
                public string PhysicalAddress { get; set; }
                public string Type { get; set; }
            }
        }

        public class EventLog
        {
            public string Name { get; set; }
            public IList<EventLogEntry> Entries { get; set; }

            public EventLog()
            {
                this.Entries = new List<EventLogEntry>();
            }

            public class EventLogEntry
            {
                public DateTime Created { get; set; }
                public string EntryType { get; set; }
                public string Source { get; set; }
                public string Message { get; set; }
            }
        }

        public class LocalUser
        {
            public string Username { get; set; }
            public string Domain { get; set; }
        }

        public class LocalProcess
        {
            public string MainWindowTitle { get; set; }
            public string ProcessName { get; set; }
            public DateTime? StartTime { get; set; }
            public string FileName { get; set; }
            public string Owner { get; set; }
            public string OwnerDomain { get; set; }
            public string OwnerSid { get; set; }
        }

        public class DriveInfo
        {
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

        public class Port
        {
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
    */
}