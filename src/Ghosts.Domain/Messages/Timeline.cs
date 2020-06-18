// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Domain
{
    /// <summary>
    ///     an array of events that a client should perform in order to best mimic some persona x
    /// </summary>
    public class Timeline
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TimelineStatus
        {
            Run,
            Stop
        }

        public Timeline()
        {
            TimeLineHandlers = new List<TimelineHandler>();
        }

        /// <summary>
        ///     Run or Stop
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TimelineStatus Status { get; set; }

        public List<TimelineHandler> TimeLineHandlers { get; set; }
    }

    /// <summary>
    ///     an array of application events that a client will execute - aka "randomly browse 6 different web pages for new shoes at 0900"
    /// </summary>
    public class TimelineHandler
    {
        public TimelineHandler()
        {
            TimeLineEvents = new List<TimelineEvent>();
            HandlerArgs = new Dictionary<string, string>();
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public HandlerType HandlerType { get; set; }

        /// <summary>
        ///     Used to instantiate browser object
        /// </summary>
        public string Initial { get; set; }

        public TimeSpan UtcTimeOn { get; set; }
        public TimeSpan UtcTimeOff { get; set; }

        //not required currently (2.4)
        public Dictionary<string, string> HandlerArgs { get; set; }

        public bool Loop { get; set; }

        public List<TimelineEvent> TimeLineEvents { get; set; }
    }

    /// <summary>
    ///     handlers map to applications
    /// </summary>
    public enum HandlerType
    {
        BrowserIE = 0,
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
        Bash = 40,
        Print = 45
    }

    /// <summary>
    ///     The specific events that a handler will execute
    /// </summary>
    public class TimelineEvent
    {
        public TimelineEvent()
        {
            CommandArgs = new List<string>();
        }

        /// <summary>
        ///     AlertIds trace back to an alert that monitors specific activity executed within a timeline
        /// </summary>
        public string TrackableId { get; set; }

        public string Command { get; set; }
        public List<string> CommandArgs { get; set; }

        /// <summary>
        ///     Milliseconds
        /// </summary>
        public int DelayAfter { get; set; }

        /// <summary>
        ///     Milliseconds
        /// </summary>
        public int DelayBefore { get; set; }
    }

    /// <summary>
    ///     Gets passed back to api server 'Chrome, Browse, http://cnn.com'
    /// </summary>
    public class TimeLineRecord
    {
        public string Handler { get; set; }
        public string Command { get; set; }
        public string CommandArg { get; set; }
        public string TrackableId { get; set; }
        public string Result { get; set; }
    }
}