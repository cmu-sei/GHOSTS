// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// What POST api/scenarios/import just dropped, from the schema README's "What the API cannot hold"
/// table. Validate never calls this — it describes what an import did, and validate does not write
/// anything — so it only runs after a document has passed the validator and CreateAsync has
/// succeeded. It reports content the document actually carries, not every row of the table
/// unconditionally: a document with no <c>catalog</c> block does not need to be told catalog has no
/// column.
/// </summary>
public static class StorageLossAnalyzer
{
    private const int Tier = 4;

    /// <summary>One STORAGE_LOSSY finding per top-level document path that lost something, each
    /// listing the leaf paths under it that this document actually populated.</summary>
    public static IReadOnlyList<ScenarioFinding> Analyze(JsonObject doc)
    {
        var findings = new List<ScenarioFinding>();

        AddWhole(findings, doc, "/slug", "slug");
        AddWhole(findings, doc, "/intent", "intent");
        AddWhole(findings, doc, "/catalog", "catalog");

        var audience = Obj(doc, "audience");
        AddLeaves(findings, "/audience",
            new[] { "role", "size", "proficiency", "mandate" }.Where(k => Present(audience, k)));

        var adversaryLeaves = new List<string>();
        foreach (var a in Arr(doc, "adversaries"))
        {
            var o = a as JsonObject;
            if (Present(o, "objective")) adversaryLeaves.Add("objective");
            if (Present(o, "winThreshold")) adversaryLeaves.Add("winThreshold");
            if (Present(o, "playbook")) adversaryLeaves.Add("playbook[]");
        }
        AddLeaves(findings, "/adversaries", adversaryLeaves);

        var terrain = Obj(doc, "terrain");
        var terrainLeaves = new List<string>();
        foreach (var key in new[] { "reference", "segments", "hosts", "services", "informationEnvironment" })
        {
            if (Present(terrain, key)) terrainLeaves.Add(key == "reference" || key == "informationEnvironment" ? key : $"{key}[]");
        }
        foreach (var d in Arr(terrain, "defenses"))
        {
            var o = d as JsonObject;
            if (Present(o, "description")) terrainLeaves.Add("defenses[].description");
            if (Present(o, "covers")) terrainLeaves.Add("defenses[].covers");
        }
        AddLeaves(findings, "/terrain", terrainLeaves);

        var populationLeaves = new List<string>();
        foreach (var p in Arr(Obj(doc, "population"), "pools"))
        {
            if (Present(p as JsonObject, "description")) { populationLeaves.Add("pools[].description"); break; }
        }
        AddLeaves(findings, "/population", populationLeaves);

        var startingLeaves = new List<string>();
        var starting = Obj(doc, "startingConditions");
        if (Present(starting, "flags")) startingLeaves.Add("flags[]");
        if (Present(starting, "facts")) startingLeaves.Add("facts");
        AddLeaves(findings, "/startingConditions", startingLeaves);

        var rop = Obj(doc, "rulesOfPlay");
        var ropLeaves = new List<string>();
        if (Present(rop, "clock")) ropLeaves.Add("clock");
        if (Present(rop, "deadline")) ropLeaves.Add("deadline");
        if (Present(rop, "fog")) ropLeaves.Add("fog");
        if (Present(Obj(rop, "escalationLadder"), "rungs")) ropLeaves.Add("escalationLadder.rungs[]");
        AddLeaves(findings, "/rulesOfPlay", ropLeaves);

        var objectiveLeaves = new List<string>();
        foreach (var o in Arr(Obj(doc, "assessment"), "objectives"))
        {
            if (Present(o as JsonObject, "metWhen")) { objectiveLeaves.Add("objectives[].metWhen"); break; }
        }
        AddLeaves(findings, "/assessment", objectiveLeaves);

        var events = Arr(Obj(doc, "timeline"), "events").Select(e => e as JsonObject).ToList();
        var timelineLeaves = new List<string>();
        // Every event has a required id (schema), and the event row has no id column: the id is
        // always rebuilt as event-{number}. So this leaf is unconditional once any event exists.
        if (events.Count > 0) timelineLeaves.Add("events[].id");
        foreach (var e in events)
        {
            // Title survives only on an inject (title, no description). Once description is also
            // present the event maps to a timeline-event row, which has no title column.
            if (Present(e, "description") && Present(e, "title")) timelineLeaves.Add("events[].title");
            if (Present(e, "expectedResponse")) timelineLeaves.Add("events[].expectedResponse");
            if (Present(e, "effects")) timelineLeaves.Add("events[].effects");
            if (Present(e, "indicators")) timelineLeaves.Add("events[].indicators");
        }
        AddLeaves(findings, "/timeline", timelineLeaves);

        AddWhole(findings, doc, "/sources", "sources[]");
        AddWhole(findings, doc, "/references", "references[]");

        return findings;
    }

    /// <summary>A finding for a top-level path that is dropped as a whole (no sub-leaves to list).</summary>
    private static void AddWhole(List<ScenarioFinding> findings, JsonObject doc, string path, string leaf)
    {
        var key = path.TrimStart('/');
        if (Present(doc, key)) AddLeaves(findings, path, [leaf]);
    }

    private static void AddLeaves(List<ScenarioFinding> findings, string path, IEnumerable<string> leaves)
    {
        var list = leaves.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (list.Count == 0) return;
        findings.Add(ScenarioFinding.Warn(Tier, "STORAGE_LOSSY", path,
            $"No column for: {string.Join(", ", list)}. Import wrote the rest of this document and did not write these.",
            "Expected, not a defect — see the schema README's \"What the API cannot hold\" table. An export of this scenario will not return them either."));
    }

    /// <summary>True when the key is present with content: a non-empty string, a non-empty array or
    /// object, or any number/boolean (an authored 0 or false is still an authored value).</summary>
    private static bool Present(JsonObject obj, string key) =>
        (obj?[key]) switch
        {
            null => false,
            JsonArray a => a.Count > 0,
            JsonObject o => o.Count > 0,
            JsonValue v when v.TryGetValue<string>(out var s) => !string.IsNullOrEmpty(s),
            JsonValue => true,
            _ => false
        };

    private static JsonObject Obj(JsonObject parent, string key) => parent?[key] as JsonObject;

    private static IEnumerable<JsonNode> Arr(JsonObject parent, string key) =>
        parent?[key] is JsonArray a ? a.Where(n => n != null) : [];
}
