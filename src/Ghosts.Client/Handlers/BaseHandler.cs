// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;
using NLog;
using Newtonsoft.Json;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseHandler
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");
        internal static readonly Random _random = new Random();

        public void Init(TimelineHandler handler)
        {
            WorkingHours.Is(handler);
        }

        public void Report(string handler, string command, string arg)
        {
            Report(handler, command, arg, null, null);
        }

        public void Report(string handler, string command, string arg, string trackable)
        {
            Report(handler, command, arg, trackable, null);
        }

        public void Report(string handler, string command, string arg, string trackable, string result)
        {
            var record = new TimeLineRecord();
            record.Handler = handler;
            record.Command = command;
            record.CommandArg = arg;
            record.Result = result;

            if (!string.IsNullOrEmpty(trackable))
                record.TrackableId = trackable;

            var o = JsonConvert.SerializeObject(record,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

            _timelineLog.Info($"TIMELINE|{DateTime.UtcNow}|{o}");
        }

        public void WebhookCreate(string payload)
        {
            if (payload != null)
            {
                payload = payload.Replace(Environment.NewLine, string.Empty);
                _timelineLog.Info($"WEBHOOKCREATE|{DateTime.UtcNow}|{payload}");
            }
        }
    }
}
