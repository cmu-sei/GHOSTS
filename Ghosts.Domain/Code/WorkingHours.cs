// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using NLog;

namespace Ghosts.Domain.Code
{
    /// <summary>
    /// In and out of office hour management with 30 min of fuzz built in
    /// </summary>
    public static class WorkingHours
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Is(TimelineHandler handler)
        {
            if (handler.UtcTimeOn > new TimeSpan(0, 0, 0) && handler.UtcTimeOff > new TimeSpan(0, 0, 0)) //ignore the unset
            {
                //fuzz
                var r = new Random().Next(-30, 30);
                if (r > 0)
                    handler.UtcTimeOn = handler.UtcTimeOn.Add(new TimeSpan(0, r, new Random().Next(0, 59)));
                else
                    handler.UtcTimeOn = handler.UtcTimeOn.Subtract(new TimeSpan(0, r, new Random().Next(0, 59)));

                r = new Random().Next(-30, 30);
                if (r > 0)
                    handler.UtcTimeOff = handler.UtcTimeOff.Add(new TimeSpan(0, new Random().Next(-30, 30), new Random().Next(0, 59)));
                else
                    handler.UtcTimeOff = handler.UtcTimeOff.Subtract(new TimeSpan(0, new Random().Next(-30, 30), new Random().Next(0, 59)));

                var isOvernight = handler.UtcTimeOff < handler.UtcTimeOn;

                _log.Debug($"For {handler.HandlerType}: Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay} On: {handler.UtcTimeOn} Off: {handler.UtcTimeOff} Overnight? {isOvernight}");

                if (isOvernight)
                {
                    while (DateTime.UtcNow.TimeOfDay < handler.UtcTimeOn)
                    {
                        var sleep = Math.Abs((handler.UtcTimeOn - DateTime.UtcNow.TimeOfDay).TotalMilliseconds);
                        _log.Trace($"Sleeping for {sleep}");
                        //delay until start time
                        Thread.Sleep(Convert.ToInt32(sleep));
                    }
                }
                else
                {
                    while (DateTime.UtcNow.TimeOfDay < handler.UtcTimeOn ||
                           DateTime.UtcNow.TimeOfDay > handler.UtcTimeOff)
                    {
                        _log.Trace($"Sleeping for 60000");
                        Thread.Sleep(60000);
                    }
                }
            }
        }
    }
}
