// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ghosts.Api.Infrastructure.Models;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Maps a scenario between the database and a scenario document (schema v1). The document carries the
/// authored specification only: no database ids, no timestamps, no run state. schemas/scenario-document/
/// tools/scenario-doc.mjs is the reference for canonical form — keys in schema order, free-key objects
/// sorted, set-like arrays sorted, empty optional values omitted, two-space indent, trailing newline —
/// so two exports of an unchanged scenario are byte-identical.
/// </summary>
public static class ScenarioDocumentMapper
{
    public const string SchemaVersion = "1.0.0";

    private static readonly JsonSerializerOptions Canonical = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // JSON.stringify escapes only what JSON requires
    };

    // ───────────────────────── export ─────────────────────────

    /// <summary>
    /// The scenario as a canonical document. Entities, edges and objectives are passed in because
    /// the scenario read path does not include them.
    /// </summary>
    public static JsonObject ToDocument(
        Scenario s,
        IReadOnlyList<ScenarioEntity> entities,
        IReadOnlyList<ScenarioEdge> edges,
        IReadOnlyList<Objective> objectives)
    {
        var p = s.ScenarioParameters;
        var te = s.TechnicalEnvironment;
        var gm = s.GameMechanics;
        var tl = s.ScenarioTimeline;

        // Objectives get document-local ids: 1..n in display order. Events refer to them.
        var ordered = (objectives ?? []).OrderBy(o => o.SortOrder).ThenBy(o => o.Id).ToList();
        var localId = new Dictionary<int, int>();
        for (var i = 0; i < ordered.Count; i++) localId[ordered[i].Id] = i + 1;

        var doc = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["slug"] = Slugify(s.Name),
            ["name"] = s.Name ?? string.Empty,
            ["description"] = s.Description ?? string.Empty
        };

        var context = new JsonObject();
        Put(context, "political", p?.PoliticalContext);
        Put(doc, "context", context);

        var audience = new JsonObject();
        Put(audience, "rulesOfEngagement", p?.RulesOfEngagement);
        Put(doc, "audience", audience);

        if (p != null)
        {
            var sides = new JsonArray();
            foreach (var n in p.Nations.OrderBy(x => x.Id))
            {
                sides.Add(new JsonObject { ["name"] = n.Name ?? string.Empty, ["alignment"] = Lower(n.Alignment) });
            }
            Put(doc, "sides", sides);

            var adversaries = new JsonArray();
            foreach (var a in p.ThreatActors.OrderBy(x => x.Id))
            {
                var ttps = SplitList(a.Ttps);
                var o = new JsonObject { ["id"] = Slugify(a.Name), ["name"] = a.Name ?? string.Empty };
                Put(o, "type", a.Type);
                if (a.Capability > 0) o["capability"] = a.Capability;
                Put(o, "techniques", Strings(ttps.Where(IsTechnique).OrderBy(t => t, StringComparer.Ordinal)));
                Put(o, "capabilities", Strings(ttps.Where(t => !IsTechnique(t))));
                adversaries.Add(o);
            }
            Put(doc, "adversaries", adversaries);
        }

        if (te != null)
        {
            var terrain = new JsonObject();
            var summary = new JsonObject();
            Put(summary, "topology", te.NetworkTopology);
            Put(summary, "services", te.Services);
            Put(summary, "assets", te.Assets);
            Put(terrain, "summary", summary);

            var defenses = new JsonArray();
            foreach (var d in ParseStringList(te.Defenses).Where(d => !string.IsNullOrEmpty(d)))
            {
                defenses.Add(new JsonObject { ["name"] = d });
            }
            Put(terrain, "defenses", defenses);

            var vulnerabilities = new JsonArray();
            foreach (var v in te.Vulnerabilities.OrderBy(x => x.Id))
            {
                var o = new JsonObject { ["asset"] = v.Asset ?? string.Empty };
                // The column is named cve but holds prose in older scenarios; plain words go to description.
                if (IsCve(v.Cve)) o["cve"] = v.Cve; else Put(o, "description", v.Cve);
                o["severity"] = Lower(v.Severity);
                vulnerabilities.Add(o);
            }
            Put(terrain, "vulnerabilities", vulnerabilities);
            Put(doc, "terrain", terrain);
        }

        if (p != null)
        {
            var pools = new JsonArray();
            foreach (var u in p.UserPools.OrderBy(x => x.Id))
            {
                pools.Add(new JsonObject { ["role"] = u.Role ?? string.Empty, ["count"] = u.Count });
            }
            var population = new JsonObject();
            Put(population, "pools", pools);
            Put(doc, "population", population);
        }

        if (gm != null)
        {
            var rop = new JsonObject();
            Put(rop, "pacing", gm.TimelineType);
            if (gm.DurationHours > 0) rop["duration"] = MinutesToDuration(gm.DurationHours * 60);
            Put(rop, "adjudication", gm.AdjudicationType);
            rop["telemetry"] = new JsonObject
            {
                ["logs"] = gm.CollectLogs,
                ["network"] = gm.CollectNetwork,
                ["endpoint"] = gm.CollectEndpoint,
                ["chat"] = gm.CollectChat
            };
            var ladder = new JsonObject();
            Put(ladder, "summary", gm.EscalationLadder);
            Put(rop, "escalationLadder", ladder);
            var branching = new JsonObject();
            Put(branching, "summary", gm.BranchingLogic);
            Put(rop, "branching", branching);
            Put(doc, "rulesOfPlay", rop);
        }

        var assessment = new JsonObject();
        Put(assessment, "purpose", p?.Objectives);
        Put(assessment, "victoryConditions", p?.VictoryConditions);
        Put(assessment, "performanceMetrics", gm?.PerformanceMetrics);
        var objectiveArray = new JsonArray();
        foreach (var o in ordered)
        {
            var item = new JsonObject { ["id"] = localId[o.Id] };
            if (o.ParentId.HasValue && localId.TryGetValue(o.ParentId.Value, out var parent)) item["parentId"] = parent;
            item["name"] = o.Name ?? string.Empty;
            Put(item, "description", o.Description);
            Put(item, "type", o.Type);
            if (o.Priority > 0) item["priority"] = o.Priority;
            Put(item, "successCriteria", o.SuccessCriteria);
            if (!string.IsNullOrEmpty(o.Assigned)) item["assigned"] = Owner(o.Assigned);
            objectiveArray.Add(item);
        }
        Put(assessment, "objectives", objectiveArray);
        Put(doc, "assessment", assessment);

        // One list of events: injects first, then timeline events.
        var events = new JsonArray();
        if (p != null)
        {
            var i = 1;
            foreach (var inj in p.Injects.OrderBy(x => x.Id))
            {
                var o = new JsonObject { ["id"] = $"inject-{i++}" };
                PutTime(o, inj.Trigger);
                o["owner"] = "white-cell";
                Put(o, "title", inj.Title);
                events.Add(o);
            }
        }
        foreach (var e in (tl?.ScenarioTimelineEvents ?? []).OrderBy(x => x.Number).ThenBy(x => x.Id))
        {
            var o = new JsonObject { ["id"] = $"event-{e.Number}" };
            PutTime(o, e.Time);
            Put(o, "schedule", e.Schedule);
            Put(o, "when", e.TriggerCondition);
            o["owner"] = Owner(e.Assigned);
            Put(o, "description", e.Description);
            var refs = ParseIntList(e.ObjectiveIds)
                .Where(localId.ContainsKey).Select(id => localId[id]).Distinct()
                // The canonicalizer sorts set-like arrays with JavaScript's default comparator,
                // which compares numbers as strings; matched here so an export is canonical.
                .OrderBy(n => n.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal).ToList();
            if (refs.Count > 0) o["objectives"] = new JsonArray(refs.Select(n => (JsonNode)JsonValue.Create(n)).ToArray());
            var execution = new JsonObject { ["mode"] = e.ExecutionType.ToString().ToLowerInvariant() };
            Put(execution, "workflowRef", e.WorkflowId);
            o["execution"] = execution;
            events.Add(o);
        }
        var timeline = new JsonObject();
        Put(timeline, "events", events);
        Put(doc, "timeline", timeline);

        if (p != null)
        {
            var workflows = new JsonArray();
            foreach (var w in p.WorkflowBindings.OrderBy(x => x.Id))
            {
                var o = new JsonObject { ["ref"] = w.WorkflowRef ?? string.Empty };
                Put(o, "displayName", w.DisplayName);
                o["cron"] = w.Cron ?? string.Empty;
                o["enabled"] = w.Enabled;
                workflows.Add(o);
            }
            Put(doc, "workflows", workflows);
        }

        // The graph: GUIDs become slug ids, edges follow. CreatedAt then Id is the only stable
        // order the tables offer; import writes CreatedAt in document order to preserve it.
        var slugs = new Dictionary<Guid, string>();
        var used = new HashSet<string>();
        var entityArray = new JsonArray();
        foreach (var en in (entities ?? []).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
        {
            var id = Slugify(en.Name);
            while (!used.Add(id)) id += "-2";
            slugs[en.Id] = id;
            var o = new JsonObject
            {
                ["id"] = id,
                ["name"] = en.Name ?? string.Empty,
                ["type"] = string.IsNullOrEmpty(en.EntityType) ? "Custom" : en.EntityType
            };
            Put(o, "description", en.Description);
            Put(o, "properties", ParseObject(en.Properties));
            Put(o, "externalId", en.ExternalId);
            Put(o, "provenance", Provenance(en.Origin, en.Confidence, en.IsReviewed));
            entityArray.Add(o);
        }
        Put(doc, "entities", entityArray);

        var edgeArray = new JsonArray();
        foreach (var ed in (edges ?? []).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
        {
            if (!slugs.TryGetValue(ed.SourceEntityId, out var from) || !slugs.TryGetValue(ed.TargetEntityId, out var to)) continue;
            var o = new JsonObject
            {
                ["from"] = from,
                ["to"] = to,
                ["type"] = string.IsNullOrEmpty(ed.EdgeType) ? "Custom" : ed.EdgeType
            };
            Put(o, "label", ed.Label);
            o["weight"] = Number(ed.Weight);
            Put(o, "properties", ParseObject(ed.Properties));
            Put(o, "provenance", Provenance(ed.Origin, ed.Confidence, ed.IsReviewed));
            edgeArray.Add(o);
        }
        Put(doc, "edges", edgeArray);

        return doc;
    }

    /// <summary>Canonical text: two-space indent, one trailing newline.</summary>
    public static string Serialize(JsonNode doc) => doc.ToJsonString(Canonical) + "\n";

    // ───────────────────────── import ─────────────────────────

    /// <summary>
    /// The document as a create request. Entities, edges and objectives ride along on the create
    /// path; the scenario is written by CreateAsync and nothing else.
    /// </summary>
    public static CreateScenarioDto FromDocument(JsonObject doc)
    {
        var context = Obj(doc, "context");
        var audience = Obj(doc, "audience");
        var terrain = Obj(doc, "terrain");
        var summary = Obj(terrain, "summary");
        var rop = Obj(doc, "rulesOfPlay");
        var assessment = Obj(doc, "assessment");
        var timeline = Obj(doc, "timeline");

        var nations = Arr(doc, "sides").Select(n => new NationDto(Str(n, "name"), Str(n, "alignment"))).ToList();

        var threatActors = Arr(doc, "adversaries").Select(a => new ThreatActorDto(
            Str(a, "name"),
            Str(a, "type"),
            Int(a, "capability"),
            StrList(a, "techniques").Concat(StrList(a, "capabilities")).ToList())).ToList();

        // Rule 5 reversed: an event with a title and no description is an inject.
        var injects = new List<InjectDto>();
        var events = new List<TimelineEventDto>();
        foreach (var e in Arr(timeline, "events"))
        {
            var at = Str(e, "at");
            var time = string.IsNullOrEmpty(at) ? Str(e, "displayTime") : at;
            var title = Str(e, "title");
            if (!string.IsNullOrEmpty(title) && string.IsNullOrEmpty(Str(e, "description")))
            {
                injects.Add(new InjectDto(time, title));
                continue;
            }
            var execution = Obj(e as JsonObject, "execution");
            var schedule = Str(e, "schedule");
            var when = Str(e, "when");
            events.Add(new TimelineEventDto(
                time,
                events.Count + 1,
                CellName(Str(e, "owner")),
                Str(e, "description"),
                "Pending",
                IntList(e, "objectives"),
                !string.IsNullOrEmpty(schedule) ? "Scheduled" : !string.IsNullOrEmpty(when) ? "Triggered" : "PointInTime",
                NullIfEmpty(schedule),
                NullIfEmpty(when),
                execution == null ? "Manual" : Capitalize(Str(execution, "mode")),
                NullIfEmpty(Str(execution, "workflowRef"))));
        }

        var pools = Arr(Obj(doc, "population"), "pools")
            .Select(u => new UserPoolDto(Str(u, "role"), Int(u, "count"))).ToList();

        // Nothing is bound silently: an absent workflows list binds nothing, so the list is never
        // null on this path — a null would seed the three defaults.
        var bindings = Arr(doc, "workflows").Select(w => new ScenarioWorkflowBindingDto(
            Str(w, "ref"), Str(w, "displayName"), Str(w, "cron"), Bool(w, "enabled"))).ToList();

        var parameters = new ScenarioParametersDto(
            nations,
            threatActors,
            injects,
            pools,
            Str(assessment, "purpose"),
            Str(context, "political"),
            Str(audience, "rulesOfEngagement"),
            Str(assessment, "victoryConditions"),
            bindings);

        var technical = new TechnicalEnvironmentDto(
            Str(summary, "topology"),
            Str(summary, "services"),
            Str(summary, "assets"),
            Arr(terrain, "defenses").Select(d => Str(d, "name")).ToList(),
            Arr(terrain, "vulnerabilities").Select(v => new VulnerabilityDto(
                Str(v, "asset"),
                string.IsNullOrEmpty(Str(v, "cve")) ? Str(v, "description") : Str(v, "cve"),
                Str(v, "severity"))).ToList());

        // One duration in the document, two columns in the database: both are written in hours
        // from it so they cannot disagree. Sub-hour and non-integral durations do not survive.
        var hours = DurationToHours(Str(rop, "duration"));
        var telemetry = Obj(rop, "telemetry");
        var mechanics = new GameMechanicsDto(
            Str(rop, "pacing"),
            hours,
            Str(rop, "adjudication"),
            Str(Obj(rop, "escalationLadder"), "summary"),
            Str(Obj(rop, "branching"), "summary"),
            new TelemetryDto(Bool(telemetry, "logs"), Bool(telemetry, "network"), Bool(telemetry, "endpoint"), Bool(telemetry, "chat")),
            Str(assessment, "performanceMetrics"));

        var entities = Arr(doc, "entities").Select(e =>
        {
            var provenance = Obj(e as JsonObject, "provenance");
            return new ScenarioEntityImportDto(
                Str(e, "id"),
                Str(e, "name"),
                Str(e, "type"),
                Str(e, "description"),
                Raw(e, "properties"),
                Str(e, "externalId"),
                Capitalize(Str(provenance, "origin")),
                Decimal(provenance, "confidence", 1.0m),
                Bool(provenance, "reviewed"));
        }).ToList();

        var edges = Arr(doc, "edges").Select(e =>
        {
            var provenance = Obj(e as JsonObject, "provenance");
            return new ScenarioEdgeImportDto(
                Str(e, "from"),
                Str(e, "to"),
                Str(e, "type"),
                Str(e, "label"),
                Decimal(e as JsonObject, "weight", 1.0m),
                Raw(e, "properties"),
                Capitalize(Str(provenance, "origin")),
                Decimal(provenance, "confidence", 1.0m),
                Bool(provenance, "reviewed"));
        }).ToList();

        var objectives = Arr(assessment, "objectives").Select((o, i) => new ScenarioObjectiveImportDto(
            Int(o, "id"),
            (o as JsonObject)?["parentId"] == null ? null : Int(o, "parentId"),
            Str(o, "name"),
            Str(o, "description"),
            Str(o, "type"),
            Int(o, "priority"),
            Str(o, "successCriteria"),
            CellName(Str(o, "assigned")),
            i)).ToList();

        return new CreateScenarioDto(
            Str(doc, "name"),
            Str(doc, "description"),
            parameters,
            technical,
            mechanics,
            new TimelineDto(hours, events),
            entities,
            edges,
            objectives);
    }

    // ───────────────────────── grammar ─────────────────────────

    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Technique = new("^T[0-9]{4}(\\.[0-9]{3})?$", RegexOptions.Compiled);
    private static readonly Regex Cve = new("^CVE-[0-9]{4}-[0-9]{4,}$", RegexOptions.Compiled);
    private static readonly Regex Offset = new("^T\\+(?=\\d)([0-9]+d)?([0-9]+h)?([0-9]+m)?$", RegexOptions.Compiled);
    private static readonly Regex Duration = new("^(?=\\d)([0-9]+d)?([0-9]+h)?([0-9]+m)?$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Owners = new()
    {
        ["White Cell"] = "white-cell",
        ["Red Team"] = "red-team",
        ["Blue Team"] = "blue-team",
        ["Green Cell"] = "green-cell",
        ["None"] = "unassigned",
        [""] = "unassigned"
    };

    public static string Slugify(string s)
    {
        var slug = NonSlug.Replace((s ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 79) slug = slug[..79];
        return slug.Length == 0 ? "x" : slug;
    }

    private static bool IsTechnique(string s) => s != null && Technique.IsMatch(s);
    private static bool IsCve(string s) => s != null && Cve.IsMatch(s);

    private static string Owner(string assigned) =>
        Owners.TryGetValue(assigned ?? string.Empty, out var o) ? o
        : Owners.TryGetValue((assigned ?? string.Empty).Trim(), out var t) ? t : "unassigned";

    private static string CellName(string owner) =>
        Owners.FirstOrDefault(kv => kv.Value == owner && kv.Key.Length > 0).Key ?? string.Empty;

    private static string MinutesToDuration(int minutes)
    {
        int d = minutes / 1440, h = minutes % 1440 / 60, m = minutes % 60;
        return (d > 0 ? $"{d}d" : string.Empty) + (h > 0 ? $"{h}h" : string.Empty) +
               (m > 0 || (d == 0 && h == 0) ? $"{m}m" : string.Empty);
    }

    /// <summary>A document duration in hours, rounded, never rounding a real duration down to none.</summary>
    private static int DurationToHours(string duration)
    {
        if (string.IsNullOrEmpty(duration) || !Duration.IsMatch(duration)) return 0;
        var m = Duration.Match(duration);
        var minutes = Part(m.Groups[1], 1440) + Part(m.Groups[2], 60) + Part(m.Groups[3], 1);
        var hours = (int)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero);
        return hours == 0 && minutes > 0 ? 1 : hours;

        static int Part(System.Text.RegularExpressions.Group g, int scale) =>
            g.Success ? int.Parse(g.Value[..^1], CultureInfo.InvariantCulture) * scale : 0;
    }

    // ───────────────────────── helpers ─────────────────────────

    private static void Put(JsonObject o, string key, string value)
    {
        if (!string.IsNullOrEmpty(value)) o[key] = value;
    }

    private static void Put(JsonObject o, string key, JsonObject child)
    {
        if (child is { Count: > 0 }) o[key] = child;
    }

    private static void Put(JsonObject o, string key, JsonArray children)
    {
        if (children is { Count: > 0 }) o[key] = children;
    }

    private static void PutTime(JsonObject o, string time)
    {
        if (string.IsNullOrEmpty(time)) return;
        if (Offset.IsMatch(time)) o["at"] = time; else o["displayTime"] = time;
    }

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    private static JsonObject Provenance(string origin, decimal confidence, bool reviewed)
    {
        var o = new JsonObject();
        Put(o, "origin", Lower(origin));
        o["confidence"] = Number(confidence);
        o["reviewed"] = reviewed;
        return o;
    }

    /// <summary>decimal(5,4) columns carry trailing zeros that JSON.stringify would never write.</summary>
    private static JsonNode Number(decimal d) => d == decimal.Truncate(d)
        ? JsonValue.Create((long)d)
        : JsonValue.Create(decimal.Parse(d.ToString("0.####", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

    private static string Lower(string s) => (s ?? string.Empty).ToLowerInvariant();

    private static List<string> SplitList(string commaSeparated) =>
        (commaSeparated ?? string.Empty).Split(',')
        .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    private static List<string> ParseStringList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static List<int> ParseIntList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>A jsonb column as an object with its keys sorted, as the canonicalizer writes them.</summary>
    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) is JsonObject o ? Sorted(o) : null; }
        catch (JsonException) { return null; }
    }

    private static JsonObject Sorted(JsonObject o)
    {
        var result = new JsonObject();
        foreach (var kv in o.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            result[kv.Key] = kv.Value is JsonObject child ? Sorted(child) : kv.Value?.DeepClone();
        }
        return result;
    }

    private static JsonObject Obj(JsonObject parent, string key) => parent?[key] as JsonObject;

    private static IEnumerable<JsonNode> Arr(JsonObject parent, string key) =>
        parent?[key] is JsonArray a ? a.Where(n => n != null) : [];

    private static string Str(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    private static int Int(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    private static bool Bool(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    private static decimal Decimal(JsonNode parent, string key, decimal fallback) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : fallback;

    private static List<string> StrList(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonArray a
            ? a.Where(n => n != null).Select(n => n.GetValue<string>()).ToList()
            : [];

    private static List<int> IntList(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonArray a
            ? a.Where(n => n != null).Select(n => n.GetValue<int>()).ToList()
            : null;

    private static string Raw(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonObject o ? o.ToJsonString() : "{}";

    private static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
