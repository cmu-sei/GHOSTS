// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

/// <summary>Who a document is for.</summary>
public enum Audience { Participants, Facilitators, Designers }

/// <summary>
/// What an audience may see of a scenario, decided before any template gets it: the decision ledger is for
/// designers; each question and draft carries its visibility in the kind file; list items carry their own.
/// A participant document can't leak exercise-control material, whatever its template asks for.
/// </summary>
public static class AudienceFilter
{
    public static JsonObject For(JsonObject document, Kind kind, Audience audience)
    {
        var doc = (JsonObject)document.DeepClone();
        var allowed = (int)audience;
        if (audience != Audience.Designers)
        {
            doc.Remove("decisions");
            doc.Remove("findings");
        }
        foreach (var q in kind.AllQuestions.Where(q => Rank(q.Visibility) > allowed))
            Remove(doc, q.Path);
        foreach (var d in kind.Drafts.Where(d => Rank(d.Visibility) > allowed))
            Remove(doc, d.Path);
        Prune(doc, allowed, top: true);
        return doc;
    }

    /// <summary>participant 0, excon 1, designer 2; an audience sees what ranks no higher than its own number.</summary>
    private static int Rank(string? visibility) => visibility switch
    {
        "excon" => 1,
        "designer" => 2,
        _ => 0
    };

    private static void Remove(JsonObject doc, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        JsonObject? node = doc;
        foreach (var part in parts[..^1])
            node = node?[part] as JsonObject;
        node?.Remove(parts[^1]);
    }

    /// <summary>Drops list items labelled above the audience, then anything the filter left empty.</summary>
    private static void Prune(JsonNode node, int allowed, bool top = false)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj.ToList())
                {
                    if (child is null)
                        continue;
                    Prune(child, allowed);
                    if (IsEmpty(child))
                        obj.Remove(key);
                }
                break;
            case JsonArray array:
                for (var i = array.Count - 1; i >= 0; i--)
                {
                    if (array[i] is JsonObject item && Rank((item["visibility"] as JsonValue)?.GetValue<string>()) > allowed)
                    {
                        array.RemoveAt(i);
                        continue;
                    }
                    if (array[i] is { } child)
                        Prune(child, allowed);
                }
                break;
        }
    }

    private static bool IsEmpty(JsonNode node) => node is JsonObject { Count: 0 } or JsonArray { Count: 0 };
}
