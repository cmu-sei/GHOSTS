// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using NLog;

namespace Ghosts.Domain.Code
{
    /// <summary>
    ///     In and out of office hour management with 30 min of fuzz built in
    /// </summary>
    public static class WorkingHours
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Is(TimelineHandler handler)
        {
            var timeOn = handler.UtcTimeOn;
            var timeOff = handler.UtcTimeOff;

            if (timeOn > new TimeSpan(0, 0, 0) && timeOff > new TimeSpan(0, 0, 0)) //ignore the unset
            {
                //fuzz
                var r = new Random().Next(-30, 30);
                if (r > 0)
                    timeOn = timeOn.Add(new TimeSpan(0, r, new Random().Next(0, 59)));
                else
                    timeOn = timeOn.Subtract(new TimeSpan(0, r, new Random().Next(0, 59)));

                r = new Random().Next(-30, 30);
                if (r > 0)
                    timeOff = timeOff.Add(new TimeSpan(0, new Random().Next(-30, 30), new Random().Next(0, 59)));
                else
                    timeOff = timeOff.Subtract(new TimeSpan(0, new Random().Next(-30, 30), new Random().Next(0, 59)));

                var isOvernight = timeOff < timeOn;

                _log.Debug(
                    $"For {handler.HandlerType}: Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay} On: {timeOn} Off: {timeOff} Overnight? {isOvernight}");

                if (isOvernight)
                    while (DateTime.UtcNow.TimeOfDay < timeOn)
                    {
                        var sleep = Math.Abs((timeOn - DateTime.UtcNow.TimeOfDay).TotalMilliseconds);
                        if (sleep > 300000)
                            sleep = 300000;
                        _log.Trace($"Sleeping for {sleep}");
                        Thread.Sleep(Convert.ToInt32(sleep));
                    }
                else
                    while (DateTime.UtcNow.TimeOfDay < timeOn ||
                           DateTime.UtcNow.TimeOfDay > timeOff)
                    {
                        _log.Trace("Sleeping for 60000");
                        Thread.Sleep(60000);
                    }
            }
        }
    }
}