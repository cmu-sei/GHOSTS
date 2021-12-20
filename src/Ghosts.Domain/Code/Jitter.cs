// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Domain.Code
{
    /// <summary>
    ///     Jitter is slight randomization of a sleep or cycle time
    ///     e.g. 5 minutes becomes ~5 minutes so that each client isn't acting on exactly the same timeline
    /// </summary>
    public static class Jitter
    {
        public static int Randomize(object baseSleepValue, object lowJitter, object highJitter)
        {
            var newSleepValue = Convert.ToInt32(baseSleepValue);

            var r = new Random().Next(Convert.ToInt32(lowJitter), Convert.ToInt32(highJitter));
            newSleepValue += r;
            if (newSleepValue < 0)
            {
                //generate a slightly increased value
                newSleepValue = Convert.ToInt32(baseSleepValue);
                newSleepValue += new Random().Next(1, 100);
            }

            return newSleepValue;
        }

        public static int Randomize(int baseSleepValue, int lowJitter, int highJitter)
        {
            var newSleepValue = baseSleepValue;

            var r = new Random().Next(lowJitter, highJitter);
            newSleepValue += r;
            if (newSleepValue < 0)
            {
                //generate a slightly increased value
                newSleepValue = baseSleepValue;
                newSleepValue += new Random().Next(1, 100);
            }

            return newSleepValue;
        }
        
        public static int Basic(int baseSleep)
        {
            //sleep with jitter
            var sleep = baseSleep;
            var r = new Random().Next(-999, 1999);
            sleep += r;
            if (sleep < 0)
                sleep = 1;
            return sleep;
        }
    }
}