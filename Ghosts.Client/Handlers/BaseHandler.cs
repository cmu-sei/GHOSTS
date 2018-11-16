// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;
using NLog;
using Newtonsoft.Json;

namespace Ghosts.Client.Handlers
{
    public abstract class BaseHandler
    {
        private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");
        
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
                payload = payload.Replace(System.Environment.NewLine, string.Empty);
                _timelineLog.Info($"WEBHOOKCREATE|{DateTime.UtcNow}|{payload}");
            }
        }
    }
}
