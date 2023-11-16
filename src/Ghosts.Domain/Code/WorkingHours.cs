// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Domain.Code
{
    public static class WorkingHours
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Is(TimelineHandler handler)
        {
            var timeOn = handler.UtcTimeOn;
            var timeOff = handler.UtcTimeOff;
            var defaultTimespan = new TimeSpan(0, 0, 0);
            var currentTime = DateTime.UtcNow.TimeOfDay;


            if (timeOn == defaultTimespan && timeOff == defaultTimespan) //ignore timelines that are unset (00:00:00)
                return;

            var isOvernight = timeOff < timeOn;

            _log.Debug($"For {handler.HandlerType}: Local time: {currentTime} UTC: {currentTime} On: {timeOn} Off: {timeOff} Overnight? {isOvernight}");

            if (isOvernight)
            {
                while (currentTime < timeOn)
                {
                    var sleep = Math.Abs((timeOn - currentTime).TotalMilliseconds);
                    if (sleep > 300000)
                        sleep = 300000;
                    Sleep(handler, Convert.ToInt32(sleep));
                }
            }
            else
            {
                while (currentTime < timeOn || currentTime > timeOff)
                {
                    Sleep(handler, 60000);
                }
            }


            // there are time blocks set and at least two of them and in multiples of two
            var isInTimeBlock = false;
            for (var i = 0; i < handler.UtcTimeBlocks.Length; i += 2)
            {
                if (i + 1 >= handler.UtcTimeBlocks.Length) break;

                var startTime = handler.UtcTimeBlocks[i];
                var endTime = handler.UtcTimeBlocks[i + 1];

                if (currentTime >= startTime && currentTime <= endTime)
                {
                    Console.WriteLine($"Current time is within the block: {startTime} to {endTime}");
                    isInTimeBlock = true;
                    break;
                }
            }

            if (!isInTimeBlock)
            {
                // Find the next start time
                TimeSpan? nextStartTime = handler.UtcTimeBlocks.Where(t => t > currentTime)
                    .OrderBy(t => t)
                    .FirstOrDefault();


                // If there's a next start time, sleep until then
                if (nextStartTime != TimeSpan.Zero)
                {
                    var sleepDuration = nextStartTime - currentTime;
                    Console.WriteLine($"Sleeping for {sleepDuration} until the next time block starts");
                    Sleep(handler, sleepDuration.Value);
                }
                else
                {
                    // Calculate sleep time until the first time block of the next day
                    var timeTillEndOfDay = TimeSpan.FromDays(1) - currentTime;
                    var timeTillFirstBlockNextDay = handler.UtcTimeBlocks[0];
                    var totalSleepTime = timeTillEndOfDay + timeTillFirstBlockNextDay;

                    Console.WriteLine($"No more time blocks for today. Sleeping for {totalSleepTime} until the next time block starts");
                    Sleep(handler, totalSleepTime);
                }
            }
        }
        
        private static void Sleep(TimelineHandler handler, int msToSleep)
        {
            _log.Trace($"{handler} sleeping for {msToSleep} ms");
            Thread.Sleep(msToSleep);
        }

        private static void Sleep(TimelineHandler handler, TimeSpan sleep)
        {
            // TODO: We need a way to kill this handler like handler.SafeKill();
            Sleep(handler, (int)sleep.TotalMilliseconds);
        }
    }
}
