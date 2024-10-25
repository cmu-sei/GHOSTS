// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Lite.Infrastructure.Services;

public static class LogWriter
{
    private static readonly Logger _timelineLog = LogManager.GetLogger("TIMELINE");

    public static void Timeline(TimeLineRecord result)
    {
        var o = JsonConvert.SerializeObject(result,
            Formatting.None,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

        _timelineLog.Info($"TIMELINE|{DateTime.UtcNow}|{o}");
    }
}
