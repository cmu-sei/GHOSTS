// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;

namespace Ghosts.Animator.Extensions
{
    public static class NumberExtensions
    {
        public static IEnumerable<int> To(this int from, int to)
        {
            if (to >= from)
            {
                for (var i = from; i <= to; i++)
                {
                    yield return i;
                }
            }
            else
            {
                for (var i = from; i >= to; i--)
                {
                    yield return i;
                }
            }
        }

        public static int GetNumberByDecreasingWeights(this double value, int startPosition, int maxPosition, double weightFactor)
        {
            double min = 0;

            while (true)
            {
                if (startPosition == maxPosition) return maxPosition;
                var limit = (1 - min) * weightFactor + min;
                if (value < limit) return startPosition;
                startPosition++;
                min = limit;
            }
        }

        public static bool ChanceOfThisValue(this double value)
        {
            return new Random().NextDouble() >= (1 - value);
        }
    }
}
