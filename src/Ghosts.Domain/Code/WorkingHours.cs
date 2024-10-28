// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using NLog;

namespace Ghosts.Domain.Code
{
    public static class WorkingHours
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Is(TimelineHandler handler)
        {
            var utcNow = DateTime.UtcNow;
            var today = utcNow.Date;
            var timeOnUtc = today.Add(handler.UtcTimeOn);
            var timeOffUtc = today.Add(handler.UtcTimeOff);
            var isOvernight = timeOffUtc < timeOnUtc;

            if (handler.UtcTimeOn == TimeSpan.Zero && handler.UtcTimeOff == TimeSpan.Zero) // ignore timelines that are unset
                return;

            _log.Debug($"For {handler.HandlerType}: Current UTC: {utcNow} On: {timeOnUtc} Off: {timeOffUtc} Overnight? {isOvernight}");

            // Adjust times for overnight schedules
            if (isOvernight && utcNow > timeOffUtc)
            {
                timeOnUtc = timeOnUtc.AddDays(1);
                timeOffUtc = timeOffUtc.AddDays(1);
            }

            // Determine next action time
            var nextActionTime = (utcNow < timeOnUtc) ? timeOnUtc : (utcNow > timeOffUtc && !isOvernight) ? timeOnUtc.AddDays(1) : DateTime.MaxValue;

            // Handle custom time blocks
            if (handler.UtcTimeBlocks != null && handler.UtcTimeBlocks.Length >= 2)
            {
                var isInTimeBlock = false;
                for (var i = 0; i < handler.UtcTimeBlocks.Length; i += 2)
                {
                    if (i + 1 >= handler.UtcTimeBlocks.Length) break;

                    var startTime = today.Add(handler.UtcTimeBlocks[i]);
                    var endTime = today.Add(handler.UtcTimeBlocks[i + 1]);

                    if (utcNow >= startTime && utcNow <= endTime)
                    {
                        Console.WriteLine($"Current time is within the block: {startTime} to {endTime}");
                        isInTimeBlock = true;
                        break;
                    }

                    if (startTime > utcNow && startTime < nextActionTime)
                    {
                        nextActionTime = startTime;
                    }
                }

                if (!isInTimeBlock && nextActionTime == DateTime.MaxValue) // If not in a block and no next action time was found
                {
                    var nextStartTime = handler.UtcTimeBlocks.Where(t => today.Add(t) > utcNow).Min();
                    nextActionTime = today.Add(nextStartTime);
                }
            }

            // Calculate sleep duration if needed
            if (nextActionTime == DateTime.MaxValue) return;
            var sleepDuration = (int)(nextActionTime - utcNow).TotalMilliseconds;
            Console.WriteLine($"Sleeping for {sleepDuration} milliseconds until the next action time.");
            Sleep(handler, sleepDuration);
        }

        private static void Sleep(TimelineHandler handler, int msToSleep)
        {
            _log.Trace($"{handler} sleeping for {msToSleep} ms");
            Thread.Sleep(msToSleep);
        }
    }
}
