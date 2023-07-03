// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using NLog;
using Ghosts.Domain;

namespace ghosts.client.linux.Infrastructure;

/// <summary>
/// This largely replicates the same class in Domain, 
/// but adds a kill switch for the handler so that 
/// when out of office, the app is sure to be closed
/// </summary>
public static class WorkingHours
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void Is(TimelineHandler handler)
    {
        var timeOn = handler.UtcTimeOn;
        var timeOff = handler.UtcTimeOff;
        var defaultTimespan = new TimeSpan(0, 0, 0);

        if (timeOn == defaultTimespan && timeOff == defaultTimespan) //ignore timelines that are unset (00:00:00)
            return;

        var isOvernight = timeOff < timeOn;

        _log.Debug(
            $"For {handler.HandlerType}: Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay} On: {timeOn} Off: {timeOff} Overnight? {isOvernight}");

        if (isOvernight)
        {
            while (DateTime.UtcNow.TimeOfDay < timeOn)
            {
                var sleep = Math.Abs((timeOn - DateTime.UtcNow.TimeOfDay).TotalMilliseconds);
                if (sleep > 300000)
                    sleep = 300000;
                Sleep(handler, Convert.ToInt32(sleep));
            }
        }
        else
        {
            while (DateTime.UtcNow.TimeOfDay < timeOn ||
                   DateTime.UtcNow.TimeOfDay > timeOff)
            {
                Sleep(handler, 60000);
            }
        }
    }

    private static void Sleep(TimelineHandler handler, int sleep)
    {
        _log.Trace($"Sleeping for {sleep} and killing processes...");
        //TODO - have to port the ProcessManager stuff
        //ProcessManager.KillProcessAndChildrenByHandler(handler);
        Thread.Sleep(sleep);
    }
}