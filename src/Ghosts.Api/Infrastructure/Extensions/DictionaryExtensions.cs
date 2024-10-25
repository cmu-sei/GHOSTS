// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Text;

namespace ghosts.api.Infrastructure.Extensions;

public static class DictionaryExtensions
{
    public static string ToSafeString(this Dictionary<string, string> payload)
    {
        if (payload == null)
        {
            return "null";
        }

        var sb = new StringBuilder();
        sb.Append("{ ");
        foreach (var kvp in payload)
        {
            // Safely handle null keys or values
            var key = kvp.Key ?? "null";
            var value = kvp.Value ?? "null";
            sb.AppendFormat("[{0}: {1}], ", key, value);
        }
        if (payload.Count > 0)
        {
            sb.Length -= 2; // Remove the last comma and space
        }
        sb.Append(" }");
        return sb.ToString();
    }
}
