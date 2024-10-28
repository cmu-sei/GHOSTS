// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Domain
{
    /// <summary>
    /// an array of events that a client should perform in order to best mimic some persona x
    /// </summary>
    public class Timeline
    {
        public Timeline()
        {
            TimeLineHandlers = new List<TimelineHandler>();
        }

        /// <summary>
        /// Useful for tracking where activity on a client originated
        /// </summary>
        public Guid Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TimelineStatus
        {
            Run,
            Stop
        }

        /// <summary>
        /// Run or Stop
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TimelineStatus Status { get; set; }

        public List<TimelineHandler> TimeLineHandlers { get; set; }
    }

    /// <summary>
    /// an array of application events that a client will execute - aka "randomly browse 6 different web pages for new shoes at 0900"
    /// </summary>
    public class TimelineHandler
    {
        public TimelineHandler()
        {
            TimeLineEvents = new List<TimelineEvent>();
            HandlerArgs = new Dictionary<string, object>();
            ScheduleType = TimelineScheduleType.Other;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public HandlerType HandlerType { get; set; }

        /// <summary>
        /// Used to instantiate browser object
        /// </summary>
        public string Initial { get; set; }

        [JsonConverter(typeof(TimeSpanConverter))]
        [JsonProperty(TypeNameHandling = TypeNameHandling.All)]
        public TimeSpan UtcTimeOn { get; set; }

        [JsonConverter(typeof(TimeSpanConverter))]
        [JsonProperty(TypeNameHandling = TypeNameHandling.All)]
        public TimeSpan UtcTimeOff { get; set; }

        [JsonConverter(typeof(TimeSpanArrayConverter))]
        [JsonProperty(TypeNameHandling = TypeNameHandling.All, NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan[] UtcTimeBlocks { get; set; }

        //not required currently (2.4)
        public Dictionary<string, object> HandlerArgs { get; set; }

        public bool Loop { get; set; }

        public List<TimelineEvent> TimeLineEvents { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TimelineScheduleType
        {
            Other,
            Cron
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public TimelineScheduleType ScheduleType { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Schedule { get; set; }
    }

    /// <summary>
    /// handlers map to applications
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HandlerType
    {
        //[Obsolete("Unsupported going forward (as of v6)", false)]
        //BrowserIE = 0,
        BrowserFirefox = 1,
        BrowserChrome = 2,
        Command = 3,
        Notepad = 4,
        Outlook = 5,
        Word = 6,
        Excel = 7,
        PowerPoint = 8,
        NpcSystem = 9,
        Reboot = 10,
        Curl = 11,
        Clicks = 12,
        Watcher = 19,
        LightWord = 31,
        LightExcel = 32,
        LightPowerPoint = 33,
        PowerShell = 39,
        Bash = 40,
        Print = 45,
        Ssh = 100,
        Sftp = 101,
        Pidgin = 102,
        Rdp = 103,
        Wmi = 104,
        Outlookv2 = 105,
        Ftp = 106,
        Aws = 110,
        Azure = 120
    }

    /// <summary>
    /// The specific events that a handler will execute
    /// </summary>
    public class TimelineEvent
    {
        public TimelineEvent()
        {
            CommandArgs = new List<object>();
        }

        /// <summary>
        /// AlertIds trace back to an alert that monitors specific activity executed within a timeline
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string TrackableId { get; set; }

        public string Command { get; set; }
        public List<object> CommandArgs { get; set; }

        /// <summary>
        /// In milliseconds
        /// </summary>
        public object DelayAfter { get; set; }

        /// <summary>
        /// In milliseconds
        /// </summary>
        public object DelayBefore { get; set; }

        [JsonIgnore]
        public int DelayAfterActual => DelayAfter.GetDelay();

        [JsonIgnore]
        public int DelayBeforeActual => DelayBefore.GetDelay();
    }

    /// <summary>
    /// Gets passed back to api server 'Chrome, Browse, https://cmu.edu'
    /// </summary>
    public class TimeLineRecord
    {
        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string TrackableId { get; set; }
        public string Result { get; set; }
    }

    public class DelayRandom
    {
        [JsonProperty("random")]
        public bool Random { get; set; }

        [JsonProperty("min")]
        public int Min { get; set; }

        [JsonProperty("max")]
        public int Max { get; set; }
    }
}
