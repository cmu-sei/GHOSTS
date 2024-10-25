// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace ghosts.client.linux.handlers
{
    public abstract class BaseHandler
    {
        public static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");
        internal static readonly Random _random = new();

        public static void Init(TimelineHandler handler)
        {
            WorkingHours.Is(handler);
        }

        public static void Report(ReportItem reportItem)
        {
            var result = new TimeLineRecord
            {
                Handler = reportItem.Handler,
                Command = reportItem.Command,
                CommandArg = reportItem.Arg,
                Result = reportItem.Result,
                TrackableId = reportItem.Trackable
            };

            var o = JsonConvert.SerializeObject(result,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

            _timelineLog.Info($"TIMELINE|{DateTime.UtcNow}|{o}");
        }
    }

}
