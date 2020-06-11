// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.RegularExpressions;

namespace Ghosts.Api.Infrastructure.Extensions
{
    public static class StringExtensions
    {
        public static string ToCondensedLowerCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var startUnderscores = Regex.Match(input, @"^+");
            return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1$2").ToLower();
        }
    }
}