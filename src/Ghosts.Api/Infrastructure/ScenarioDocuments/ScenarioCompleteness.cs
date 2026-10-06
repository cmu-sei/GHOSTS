// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// What an exercise still lacks, read from its document (E5): objectives with pass conditions, the
/// audience, the rules of engagement and the rules of play. The validator checks that what a document
/// holds is right; this checks that the exercise has what it needs. It reports, and blocks nothing.
/// </summary>
public static class ScenarioCompleteness
{
    public static List<string> Gaps(JsonObject doc)
    {
        var gaps = new List<string>();

        var objectives = doc?["assessment"]?["objectives"] as JsonArray ?? new JsonArray();
        if (!objectives.Any(o => Has(o, "successCriteria") || Has(o, "metWhen")))
            gaps.Add("No objective has a pass condition (assessment.objectives[].successCriteria or metWhen).");

        if (!Has(doc?["audience"], "role"))
            gaps.Add("No training audience (audience.role).");

        if (!Has(doc?["audience"], "rulesOfEngagement"))
            gaps.Add("No rules of engagement (audience.rulesOfEngagement).");

        if (doc?["rulesOfPlay"] is not JsonObject { Count: > 0 })
            gaps.Add("No rules of play (rulesOfPlay: pacing, duration, adjudication).");

        return gaps;
    }

    private static bool Has(JsonNode node, string key) => node is JsonObject o && o[key] switch
    {
        null => false,
        JsonValue v when v.TryGetValue<string>(out var s) => !string.IsNullOrWhiteSpace(s),
        _ => true
    };
}
