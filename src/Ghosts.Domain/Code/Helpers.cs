// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ghosts.Domain.Code
{
    public static class Helpers
    {
        /// <summary>
        ///     Get name value of an Enum
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T ParseEnum<T>(string value)
        {
            return (T) Enum.Parse(typeof(T), value, true);
        }

        public static bool IsOlderThanHours(string filename, int hours)
        {
            var threshold = DateTime.Now.AddHours(-hours);
            return File.GetCreationTime(filename) <= threshold;
        }
    }

    public static class StringExtensions
    {
        public static IEnumerable<string> Split(this string o, string splitString)
        {
            return o.Split(new [] { splitString }, StringSplitOptions.None);
        }
        
        public static string GetTextBetweenQuotes(this string o)
        {
            var result = Regex.Match(o, "\"([^\"]*)\"").ToString();
            if(!string.IsNullOrEmpty(result))
                result = result.TrimStart('"').TrimEnd('"');
            return result;
        }
        
        public static string ReplaceCaseInsensitive(this string input, string search, string replacement)
        {
            var result = Regex.Replace(input, Regex.Escape(search), replacement.Replace("$", "$$"), RegexOptions.IgnoreCase);
            return result;
        }

        public static string RemoveFirstLines(this string text, int linesCount)
        {
            var lines = Regex.Split(text, "\r\n|\r|\n").Skip(linesCount);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static string RemoveWhitespace(this string input)
        {
            return new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        public static string RemoveDuplicateSpaces(this string input)
        {
            var regex = new Regex("[ ]{2,}", RegexOptions.None);
            return regex.Replace(input, " ");
        }

        public static string ToFormValueString<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return string.Join("&", dictionary.Select(x => x.Key + "=" + x.Value).ToArray());
        }
    }

    public static class EnumerableExtensions
    {
        /// <summary>
        ///     Picks 1 random object from a list T
        /// </summary>
        public static T PickRandom<T>(this IEnumerable<T> source)
        {
            return source.PickRandom(1).Single();
        }

        /// <summary>
        ///     Picks n random objects from a list T
        /// </summary>
        public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
        {
            return source.Shuffle().Take(count);
        }

        /// <summary>
        ///     Randomize a list of T objects in list
        /// </summary>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(x => Guid.NewGuid());
        }
    }
}