// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Domain.Code.Helpers
{
    public static class DictionaryExtensions
    {
        public static bool ContainsKeyWithOption(this Dictionary<string, object> options, string key, string value)
        {
            return options.ContainsKey(key) && (string)options[key] == value;
        }
    }
}
