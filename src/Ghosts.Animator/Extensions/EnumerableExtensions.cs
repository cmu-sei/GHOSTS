// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ghosts.Animator.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            var elements = source.ToArray();
            for (var i = elements.Length - 1; i >= 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                // ... except we don't really need to swap it fully, as we can
                // return it immediately, and afterwards it's irrelevant.
                var swapIndex = rng.Next(i + 1);
                yield return elements[swapIndex];
                elements[swapIndex] = elements[i];
            }
        }

        public static string RandomFromProbabilityList(this Dictionary<string, double> list)
        {
            var u = list.Sum(x => x.Value);
            var r = AnimatorRandom.Rand.NextDouble() * u;
            double sum = 0;
            return list.FirstOrDefault(x => r <= (sum += x.Value)).Key;
        }

        public static int GetWeightedRandomProbabilityResult(this Dictionary<string, int> probabilitySettings)
        {
            var value = AnimatorRandom.Rand.Next(100);
            var cumulativeProbability = 0;

            foreach (var probability in probabilitySettings)
            {
                cumulativeProbability += probability.Value;
                if (value < cumulativeProbability)
                {
                    try
                    {
                        return Convert.ToInt32(probability.Key);
                    }
                    catch
                    {
                        throw new Exception("The probabilities are not string, int");
                    }
                }
            }

            throw new Exception("The probabilities do not sum up to 100.");
        }

        public static int RandomFromPipedProbabilityList(this Dictionary<string, double> list)
        {
            var selected = list.RandomFromProbabilityList();
            var arr = selected.Split(Convert.ToChar("|"));
            return AnimatorRandom.Rand.Next(Convert.ToInt32(arr[0]), Convert.ToInt32(arr[1]));
        }

        public static T RandomElement<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.RandomElementUsing(new Random());
        }

        public static string Join<T>(this IEnumerable<T> items, string separator)
        {
            return items.Select(i => i.ToString())
                .Aggregate((acc, next) => string.Concat(acc, separator, next));
        }

        public static IEnumerable<T> RandPick<T>(this IEnumerable<T> items, int itemsToTake)
        {
            IList<T> list;
            if (items is IList<T> list1)
                list = list1;
            else list = items.ToList();

            var rand = AnimatorRandom.Rand;

            for (var i = 0; i < itemsToTake; i++)
                yield return list[rand.Next(list.Count)];
        }

        public static string RandomFromStringArray(this string[] ar)
        {
            var rand = AnimatorRandom.Rand;
            return ar[rand.Next(ar.Length)];
        }

        private static T RandomElementUsing<T>(this IEnumerable<T> enumerable, Random rand)
        {
            var e = enumerable.ToList();
            if (e.Count == 0)
            {
                return default;
            }
            return e.ElementAt(rand.Next(0, e.Count));
        }
    }
}
