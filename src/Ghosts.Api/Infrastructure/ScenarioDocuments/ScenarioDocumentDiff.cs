// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// The change list between two versions of a document: one line per JSON Pointer whose value changed,
/// such as "/timeline/events/1/at: T+5h30m → T+6h" (D2). It is computed from the documents themselves,
/// so it is the server's account of a revision, not the agent's.
/// </summary>
public static class ScenarioDocumentDiff
{
    public const int MaxLines = 200;
    private const int MaxValue = 80;

    /// <summary>For reading, not for a browser: "T+6h" stays "T+6h".</summary>
    public static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static List<string> Changes(JsonNode before, JsonNode after)
    {
        var lines = new List<string>();
        Walk(string.Empty, before, after, lines);
        if (lines.Count <= MaxLines) return lines;

        var more = lines.Count - MaxLines;
        return [.. lines.Take(MaxLines), $"… and {more} more changes"];
    }

    private static void Walk(string path, JsonNode a, JsonNode b, List<string> lines)
    {
        if (JsonNode.DeepEquals(a, b)) return;

        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var (key, value) in ob)
            {
                var at = $"{path}/{Escape(key)}";
                if (oa.TryGetPropertyValue(key, out var old)) Walk(at, old, value, lines);
                else lines.Add($"{at}: (none) → {Show(value)}");
            }
            foreach (var (key, value) in oa.Where(kv => !ob.ContainsKey(kv.Key)))
                lines.Add($"{path}/{Escape(key)}: {Show(value)} → (removed)");
            return;
        }

        if (a is JsonArray aa && b is JsonArray ab)
        {
            for (var i = 0; i < ab.Count; i++)
            {
                if (i < aa.Count) Walk($"{path}/{i}", aa[i], ab[i], lines);
                else lines.Add($"{path}/{i}: (none) → {Show(ab[i])}");
            }
            for (var i = ab.Count; i < aa.Count; i++)
                lines.Add($"{path}/{i}: {Show(aa[i])} → (removed)");
            return;
        }

        lines.Add($"{(path.Length == 0 ? "/" : path)}: {Show(a)} → {Show(b)}");
    }

    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");

    private static string Show(JsonNode node)
    {
        var text = node switch
        {
            null => "null",
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            _ => node.ToJsonString(Readable)
        };
        return text.Length <= MaxValue ? text : text[..MaxValue] + "…";
    }
}
