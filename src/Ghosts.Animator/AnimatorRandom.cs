// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Animator
{
    public static class AnimatorRandom
    {
        public static Random Rand = new Random();

        public static void Seed(int seed)
        {
            Rand = new Random(seed);
        }

        public static DateTime Date(int yearsAgo = 20)
        {
            var minutesAgo = yearsAgo * 525949;
            return DateTime.Now.AddMinutes(-Rand.Next(30000, minutesAgo)); //make it at least ~ month ago
        }
    }

    public static class PercentOfRandom
    {
        public static bool Does(int percentOfPeopleDo)
        {
            return (AnimatorRandom.Rand.Next(0, 100)) > (100 - percentOfPeopleDo);
        }
    }
}
