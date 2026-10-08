// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

public sealed record ReadinessGroup(string Key, string Label);

/// <summary>
/// One of ELICITATION.md's fourteen questions as the dashboard shows it. Status is <c>stated</c> or
/// <c>proposed</c> when the ledger in the agent's reply has a row for it, <c>drafted</c> when only the document
/// answers it, and <c>open</c> when neither does.
/// </summary>
public sealed record ReadinessQuestion(string Tag, string Group, string Label, string Question, string Status, string Value);

public sealed record ReadinessLedger(int Stated, int Proposed, int Open);

/// <summary>A finding sorted the way the prompt sorts it: mechanical, judgment (with the question it implies), or accepted.</summary>
public sealed record ReadinessFinding(string Severity, string Code, string Path, string Kind, string Asks);

public sealed record ReadinessFindings(int Errors, int Warnings, int Info, IReadOnlyList<ReadinessFinding> Items);

public sealed record ReadinessCount(string Label, int Count);

public sealed record ReadinessClock(string Duration, string Pacing, string Deadline, string Events);

public sealed record ReadinessReport(
    IReadOnlyList<ReadinessGroup> Groups,
    IReadOnlyList<ReadinessQuestion> Questions,
    int Answered,
    int Open,
    ReadinessLedger Ledger,
    ReadinessFindings Findings,
    IReadOnlyList<ReadinessCount> Coverage,
    ReadinessClock Clock);

/// <summary>
/// How far a scenario is from done, read from the latest document and the ledger in the agent's latest reply:
/// which of the fourteen interview questions are settled and by whom, what the validator still objects to and
/// which objections are really questions, and what the document holds. Like ScenarioCompleteness it reports
/// and blocks nothing; the import rule is ImportRefusal's.
/// </summary>
public static class ScenarioReadiness
{
    public const string Stated = "stated";
    public const string Proposed = "proposed";
    public const string Drafted = "drafted";
    public const string Open = "open";

    public static readonly IReadOnlyList<ReadinessGroup> Groups =
    [
        new("A", "Audience"),
        new("B", "Decisions forced"),
        new("C", "End state"),
        new("D", "What may fail"),
        new("E", "Constraints"),
    ];

    private sealed record Definition(string Tag, string Label, string Question, Func<JsonObject, string> Excerpt);

    // The fourteen, with the document paths each one fills (ELICITATION.md). Excerpt returns what the document
    // holds for the question, or null when it holds nothing.
    private static readonly Definition[] Definitions =
    [
        new("A1", "Who is in the room", "Who is in the room, and what are they responsible for?",
            d => Str(d?["audience"]?["role"]) is { } role
                ? Join(role, Int(d["audience"]["size"]) is { } n ? $"{n} people" : null) : null),
        new("A2", "Proficiency", "What can they already do, and what will they be doing for the first time here?",
            d => Str(d?["audience"]?["proficiency"])),
        new("A3", "Simulated vs played", "Who in this scenario is simulated by GHOSTS, and who is a person playing?",
            d => Arr(d?["population"]?["pools"]) is { Count: > 0 } pools
                ? $"{pools.Count} pool{Plural(pools.Count)}: " + string.Join(", ", pools.Select(p => Join(Str(p?["role"]), Int(p?["count"])?.ToString()))) : null),
        new("B1", "Decision + deadline", "What decision must this exercise force, and by when?",
            d => Arr(d?["assessment"]?["objectives"]) is { Count: > 0 } objectives
                ? Join($"{objectives.Count} objective{Plural(objectives.Count)}",
                    d["rulesOfPlay"]?["deadline"] is JsonObject deadline ? "deadline " + Join(Str(deadline["at"]), Str(deadline["label"])) : "no deadline")
                : null),
        new("B2", "What makes it correct", "What makes that decision correct, what counts as a partial pass, and does the network have to end up clean?",
            d => Str(d?["assessment"]?["victoryConditions"])
                 ?? (Arr(d?["assessment"]?["objectives"])?.Count(o => o?["metWhen"] != null) is > 0 and var met ? $"{met} objective{Plural(met)} with metWhen" : null)),
        new("B3", "Injects + adjudication", "What puts the decision in front of them, and who decides whether their response worked?",
            d =>
            {
                var events = Arr(d?["timeline"]?["events"]);
                var adjudication = Str(d?["rulesOfPlay"]?["adjudication"]);
                if (events is not { Count: > 0 } && adjudication == null) return null;
                return Join($"{events?.Count ?? 0} event{Plural(events?.Count ?? 0)}", adjudication != null ? "adjudication: " + adjudication : "no adjudication");
            }),
        new("C1", "Last recoverable rung", "How far may the adversary get, and which is the last point where recovery is still possible?",
            d =>
            {
                if (Arr(d?["rulesOfPlay"]?["escalationLadder"]?["rungs"]) is not { Count: > 0 } rungs) return null;
                var last = rungs.LastOrDefault(r => r?["recoverable"]?.GetValue<bool>() == true);
                return Join($"{rungs.Count} rung{Plural(rungs.Count)}", last != null ? "last recoverable: " + Str(last["name"]) : "none marked recoverable");
            }),
        new("C2", "Early success", "If they succeed early, does the exercise end, or does the adversary try again?",
            d => Str(d?["rulesOfPlay"]?["branching"]?["summary"])),
        new("D1", "Rules of engagement", "What is the audience allowed to do, and what will the adversary not do?",
            d => Str(d?["audience"]?["rulesOfEngagement"])),
        new("D2", "Adversary capability", "How hard does the adversary press, and what is it actually trying to achieve?",
            d => Arr(d?["adversaries"])?.FirstOrDefault(a => a?["capability"] != null || Str(a?["objective"]) != null) is { } a
                ? Join(Str(a["name"]), Int(a["capability"]) is { } c ? $"capability {c}" : null, Str(a["objective"])) : null),
        new("D3", "Visibility / blindness", "What must they be able to see, and is there anywhere they are meant to be blind?",
            d =>
            {
                var events = Arr(d?["timeline"]?["events"]);
                var withIndicators = events?.Count(e => Arr(e?["indicators"]) is { Count: > 0 }) ?? 0;
                var defenses = Arr(d?["terrain"]?["defenses"])?.Count ?? 0;
                var fog = Str(d?["rulesOfPlay"]?["fog"]);
                if (withIndicators == 0 && defenses == 0 && fog == null && d?["rulesOfPlay"]?["telemetry"] == null) return null;
                return Join(events is { Count: > 0 } ? $"{withIndicators} of {events.Count} events have indicators" : null,
                    fog != null ? "fog " + fog : null, $"{defenses} defense{Plural(defenses)}");
            }),
        new("E1", "Duration + pacing", "How long does it run, and is that real time or compressed?",
            d => Join(Str(d?["rulesOfPlay"]?["duration"]), Str(d?["rulesOfPlay"]?["pacing"]))),
        new("E2", "Terrain", "What terrain already exists, what does this exercise add, and what may not be touched?",
            d =>
            {
                var slice = Str(d?["terrain"]?["reference"]?["slice"]);
                var segments = Arr(d?["terrain"]?["segments"])?.Count ?? 0;
                var hosts = Arr(d?["terrain"]?["hosts"])?.Count ?? 0;
                if (slice == null && segments == 0 && hosts == 0) return null;
                return Join(slice, $"{segments} segment{Plural(segments)}", $"{hosts} host{Plural(hosts)}");
            }),
        new("E3", "Real or fictional", "Is the adversary a real named actor or a fictional one, and whose country is this?",
            d =>
            {
                var names = Arr(d?["adversaries"])?.Select(a => Str(a?["name"])).Where(n => n != null).ToList();
                var sides = Arr(d?["sides"])?.Select(s => Str(s?["name"])).Where(n => n != null).ToList();
                var political = Str(d?["context"]?["political"]);
                if (names is not { Count: > 0 } && sides is not { Count: > 0 } && political == null) return null;
                return Join(names is { Count: > 0 } ? string.Join(", ", names) : null,
                    sides is { Count: > 0 } ? "sides: " + string.Join(", ", sides) : null, political);
            }),
    ];

    // The prompt's "Judgment — escalate with the question" table: the question each code implies.
    private static readonly Dictionary<string, string> Judgment = new()
    {
        ["TIME_DURATION_NOT_STORABLE"] = "E1",
        ["TIME_OUTSIDE_DURATION"] = "E1 or B3",
        ["ATTACK_DEPRECATED_TECHNIQUE"] = "D2",
        ["REF_UNSET_FLAG"] = "B3",
        ["DRYRUN_NPCS"] = "A3",
        ["TERRAIN_UNSTRUCTURED"] = "E2",
        ["TIME_NO_DURATION"] = "E1",
    };

    // The prompt's "Usually accepted" table. DRYRUN_NPCS is accepted only as info; as a warning it is judgment.
    private static readonly HashSet<string> Accepted =
    [
        "DRYRUN_LOADED", "DRYRUN_READINESS", "DRYRUN_POPULATION_SKIPPED", "DRYRUN_SKIPPED",
        "WORKFLOW_NOT_REGISTERED", "WORKFLOW_CHECK_SKIPPED",
    ];

    // A ledger row: | # | Decision | Value | Who | Why |. The decision cell opens with the question's tag when
    // the row answers one of the fourteen; rows for everything else are counted but bound to no question.
    private static readonly Regex LedgerRow = new(
        @"^\|\s*\d+\s*\|\s*(?<decision>[^|]*?)\s*\|\s*(?<value>[^|]*?)\s*\|\s*(?<who>stated|proposed)\s*\|",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex Tag = new(@"^(?<tag>[A-E][1-3])\b", RegexOptions.Compiled);

    /// <summary>Whether a reply carries ledger rows: the service hands Compute the latest one that does.</summary>
    public static bool HasLedger(string reply) => !string.IsNullOrEmpty(reply) && LedgerRow.IsMatch(reply);

    /// <param name="doc">The latest validated document, or null before the first draft.</param>
    /// <param name="findings">That document's findings, or empty.</param>
    /// <param name="reply">The agent's latest reply holding a ledger, or null.</param>
    public static ReadinessReport Compute(JsonObject doc, IReadOnlyList<ScenarioFinding> findings, string reply)
    {
        var rows = Ledger(reply);
        var questions = Definitions.Select(q =>
        {
            var excerpt = Clip(q.Excerpt(doc));
            if (rows.ByTag.TryGetValue(q.Tag, out var row))
                return new ReadinessQuestion(q.Tag, q.Tag[..1], q.Label, q.Question, row.Who, Clip(row.Value) ?? excerpt);
            return new ReadinessQuestion(q.Tag, q.Tag[..1], q.Label, q.Question, excerpt != null ? Drafted : Open, excerpt);
        }).ToList();

        var open = questions.Count(q => q.Status == Open);
        return new ReadinessReport(
            Groups,
            questions,
            questions.Count - open,
            open,
            new ReadinessLedger(rows.Stated, rows.Proposed, open),
            Sort(findings ?? []),
            Coverage(doc),
            Clock(doc));
    }

    private sealed record Row(string Who, string Value);

    private sealed record Rows(Dictionary<string, Row> ByTag, int Stated, int Proposed);

    private static Rows Ledger(string reply)
    {
        var byTag = new Dictionary<string, Row>();
        int stated = 0, proposed = 0;
        if (string.IsNullOrEmpty(reply)) return new Rows(byTag, 0, 0);
        foreach (Match m in LedgerRow.Matches(reply))
        {
            var who = m.Groups["who"].Value.ToLowerInvariant();
            if (who == Stated) stated++; else proposed++;
            var tag = Tag.Match(m.Groups["decision"].Value);
            if (tag.Success) byTag[tag.Groups["tag"].Value.ToUpperInvariant()] = new Row(who, m.Groups["value"].Value);
        }
        return new Rows(byTag, stated, proposed);
    }

    private static ReadinessFindings Sort(IReadOnlyList<ScenarioFinding> findings)
    {
        var items = findings
            .OrderBy(f => f.Severity switch { ScenarioFinding.Error => 0, ScenarioFinding.Warning => 1, _ => 2 })
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .Select(f =>
            {
                if (Judgment.TryGetValue(f.Code, out var asks) && !(f.Code == "DRYRUN_NPCS" && f.Severity == ScenarioFinding.Info))
                    return new ReadinessFinding(f.Severity, f.Code, f.Path, "judgment", asks);
                if (Accepted.Contains(f.Code) || f.Code == "DRYRUN_NPCS")
                    return new ReadinessFinding(f.Severity, f.Code, f.Path, "accepted", null);
                return new ReadinessFinding(f.Severity, f.Code, f.Path, "mechanical", null);
            })
            .ToList();
        return new ReadinessFindings(
            items.Count(f => f.Severity == ScenarioFinding.Error),
            items.Count(f => f.Severity == ScenarioFinding.Warning),
            items.Count(f => f.Severity == ScenarioFinding.Info),
            items);
    }

    private static List<ReadinessCount> Coverage(JsonObject d) =>
    [
        new("events", Arr(d?["timeline"]?["events"])?.Count ?? 0),
        new("objectives", Arr(d?["assessment"]?["objectives"])?.Count ?? 0),
        new("rungs", Arr(d?["rulesOfPlay"]?["escalationLadder"]?["rungs"])?.Count ?? 0),
        new("pools", Arr(d?["population"]?["pools"])?.Count ?? 0),
        new("entities", Arr(d?["entities"])?.Count ?? 0),
        new("segments", Arr(d?["terrain"]?["segments"])?.Count ?? 0),
        new("hosts", Arr(d?["terrain"]?["hosts"])?.Count ?? 0),
        new("techniques", Arr(d?["adversaries"])?.Sum(a => Arr(a?["techniques"])?.Count ?? 0) ?? 0),
        new("defenses", Arr(d?["terrain"]?["defenses"])?.Count ?? 0),
    ];

    private static ReadinessClock Clock(JsonObject d)
    {
        var events = Arr(d?["timeline"]?["events"]);
        var timed = events?.Count(e => e?["at"] != null) ?? 0;
        var deadline = d?["rulesOfPlay"]?["deadline"] is JsonObject dl ? Join(Str(dl["at"]), Str(dl["label"])) : null;
        return new ReadinessClock(
            Str(d?["rulesOfPlay"]?["duration"]),
            Str(d?["rulesOfPlay"]?["pacing"]),
            deadline,
            events is { Count: > 0 } ? $"{events.Count}, {timed} at a fixed time" : null);
    }

    // ───────── reading the document ─────────

    private static JsonArray Arr(JsonNode node) => node as JsonArray;

    private static string Str(JsonNode node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        JsonValue v when v.TryGetValue<int>(out var i) => i.ToString(),
        _ => null
    };

    private static int? Int(JsonNode node) => node is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    private static string Join(params string[] parts)
    {
        var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return kept.Count == 0 ? null : string.Join("; ", kept);
    }

    private static string Plural(int n) => n == 1 ? string.Empty : "s";

    /// <summary>One line on the dashboard: the first sentence or so, never a paragraph.</summary>
    private static string Clip(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = Regex.Replace(s.Trim(), @"\s+", " ");
        return s.Length <= 120 ? s : s[..117].TrimEnd() + "…";
    }
}
