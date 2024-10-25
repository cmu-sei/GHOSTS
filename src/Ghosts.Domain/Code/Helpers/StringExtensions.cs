// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ghosts.Domain.Code.Helpers
{
    public static class StringExtensions
    {
        public static IEnumerable<string> Split(this string o, string splitString)
        {
            return o.Split(new[] { splitString }, StringSplitOptions.None);
        }

        /// <summary>
        /// Regex to keep only ASCII printable characters and newlines
        /// </summary>
        public static string RemoveNonAscii(this string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                input = Regex.Replace(input, @"[^\x20-\x7E\r\n]", string.Empty);
            }
            return input;
        }

        public static string GetTextBetweenQuotes(this string o)
        {
            var result = Regex.Match(o, "\"([^\"]*)\"").ToString();
            if (!string.IsNullOrEmpty(result))
                result = result.Trim().TrimStart('"').TrimEnd('"');
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

        public static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string ToMemorySizeString(this long value)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            var adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return $"{adjustedSize:n} {SizeSuffixes[mag]}";
        }

        public static string RemoveTextBetweenMarkers(this string input, string startMarker, string endMarker)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(startMarker) || string.IsNullOrEmpty(endMarker))
            {
                return input;
            }

            input = input.TrimEnd('.');

            var result = new StringBuilder(input);
            var startIndex = 0;

            while ((startIndex = result.ToString().IndexOf(startMarker, startIndex, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                var endIndex = result.ToString().IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
                if (endIndex == -1)
                {
                    break;
                }
                endIndex += endMarker.Length;

                // Remove the substring from startIndex to endIndex
                result.Remove(startIndex, endIndex - startIndex);
            }

            return result.ToString().Replace("\n", "");
        }
    }

}
