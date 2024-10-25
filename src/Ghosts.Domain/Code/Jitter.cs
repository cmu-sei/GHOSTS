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
        private static readonly Random _random = new Random();

        public static int GetSafeSleepTime(this int x, int y)
        {
            return x - y < 0 ? 0 : x - y;
        }

        public static int Randomize(object baseSleepValue, object lowJitter, object highJitter)
        {
            var newSleepValue = Convert.ToInt32(baseSleepValue);

            var r = _random.Next(Convert.ToInt32(lowJitter), Convert.ToInt32(highJitter));
            newSleepValue += r;
            if (newSleepValue < 0)
            {
                //generate a slightly increased value
                newSleepValue = Convert.ToInt32(baseSleepValue);
                newSleepValue += _random.Next(1, 100);
            }

            return newSleepValue;
        }

        public static int Randomize(int baseSleepValue, int lowJitter, int highJitter)
        {
            var newSleepValue = baseSleepValue;

            var r = _random.Next(lowJitter, highJitter);
            newSleepValue += r;
            if (newSleepValue < 0)
            {
                //generate a slightly increased value
                newSleepValue = baseSleepValue;
                newSleepValue += _random.Next(1, 100);
            }

            return newSleepValue;
        }

        public static int Basic(int baseSleep)
        {
            //sleep with jitter
            var sleep = baseSleep;
            var r = _random.Next(-999, 1999);
            sleep += r;
            if (sleep < 0)
                sleep = 1;
            return sleep;
        }


        /// <summary>
        /// The jstring is expected to be a decimal integer string between 0 and 50
        /// which represents a +/-%window about a base value
        /// </summary>
        /// <param name="jstring"></param>
        /// <returns></returns>
        public static int JitterFactorParse(string jstring)
        {
            if (int.TryParse(jstring, out var jitterFactor))
            {
                if (jitterFactor < 0 || jitterFactor > 50) jitterFactor = 0;
            }
            else
            {
                jitterFactor = 0;
            }
            return jitterFactor;
        }

        public static int JitterFactorDelay(int baseSleep, int jitterFactor)
        {
            if (jitterFactor == 0) return baseSleep;
            return _random.Next(baseSleep - ((baseSleep * jitterFactor) / 100), baseSleep + ((baseSleep * jitterFactor) / 100));
        }


    }
}
