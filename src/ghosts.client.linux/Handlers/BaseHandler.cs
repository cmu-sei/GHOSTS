// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Domain;
using NLog;
using Newtonsoft.Json;

namespace ghosts.client.linux.handlers
{
    public abstract class BaseHandler
    {
        private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");

        protected static void Report(string handler, string command, string arg, string trackable = null)
        {
            var result = new TimeLineRecord
            {
                Handler = handler,
                Command = command,
                CommandArg = arg
            };

            if (!string.IsNullOrEmpty(trackable))
            {
                result.TrackableId = trackable;
            }

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