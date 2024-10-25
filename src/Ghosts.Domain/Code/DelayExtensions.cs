using System;
using Newtonsoft.Json.Linq;

namespace Ghosts.Domain.Code
{
    public static class DelayExtensions
    {
        public static int GetDelay(this object o)
        {
            var rnd = new Random();

            var delay = 0;

            if (o is JObject d)
            {
                // DelayAfter is an object, check for randomization
                var randomDelay = d.ToObject<DelayRandom>();
                if (randomDelay != null && randomDelay.Random)
                {
                    delay = rnd.Next(randomDelay.Min, randomDelay.Max);
                }
            }
            else if (o is int i)
            {
                delay = i;
            }
            else if (o is long l)
            {
                delay = l.SafeLongToInt();
            }

            return delay;
        }

        public static int SafeLongToInt(this long l)
        {
            if (l <= int.MaxValue && l >= int.MinValue)
            {
                return (int)l;
            }
            else
            {
                return int.MaxValue;
            }
        }
    }
}
