// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ghosts.api.Infrastructure.Extensions;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using Match = System.Text.RegularExpressions.Match;

namespace Ghosts.Api.Infrastructure.Extensions
{
    public static partial class StringExtensions
    {
        public static string ToCondensedLowerCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var startUnderscores = MyRegex().Match(input);
            return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1$2").ToLower();
        }

        public static string ReplaceDoubleQuotesWithSingleQuotes(this string input)
        {
            return input.Replace("\"", "'");
        }

        public static string Clean(this string message, IEnumerable<string> list, Random random)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var pattern = @"\b\d+\.\s(.*?)(?=(\b\d+\.|\z))";
            var matches = Regex.Matches(message, pattern);
            var listItems = new List<string>();
            foreach (Match x in matches.Cast<Match>())
            {
                listItems.Add(x.Groups[1].Value.Trim());
            }

            if (listItems.Count > 0)
                message = listItems.GetRandom(random);

            var regex = new Regex(@"""(.*?)""");
            var match = regex.Match(message);
            if (match.Success)
            {
                message = match.Groups[1].Value;
            }

            pattern = @"\[.*?\]";
            message = Regex.Replace(message, pattern, "");

            message = list.Aggregate(message, (current, s) => current.Replace(s, ""));
            var m = message.Split("\n\n");
            if (m.Length > 0)
                message = m[m.GetUpperBound(0)];
            m = message.Split(":");
            if (m.Length > 0)
                message = m[m.GetUpperBound(0)];
            message = message.Trim();
            message = message.Trim('\'', '\"', '“', '”', '\'');
            message = message.Trim();

            return message;
        }

        public static string CreateUsernameFromEmail(this string email)
        {
            if (string.IsNullOrEmpty(email)) return string.Empty;
            var username = email.Split('@')[0].Replace(".mil", "").Replace(".civ", "").Replace(".ctr", "").TrimEnd('.').TrimStart('.');
            return string.IsNullOrEmpty(email) ? string.Empty : username;
        }

        public static bool ShouldSend(this string message, IEnumerable<string> list)
        {
            return !string.IsNullOrEmpty(message) &&
                   list.All(x => !message.Contains(x, StringComparison.CurrentCultureIgnoreCase));
        }

        [GeneratedRegex(@"^+")]
        private static partial Regex MyRegex();
    }
}
