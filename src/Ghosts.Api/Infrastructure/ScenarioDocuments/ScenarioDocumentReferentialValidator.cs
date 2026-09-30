// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Tier 2: does the document hold together? Every name it uses must be a name it declares — an edge
/// endpoint, an objective, a flag, a workflow, a host, a segment, a citation — and every ATT&amp;CK id
/// must be one MITRE still recognises. The schema cannot ask these questions; they are about the
/// document as a whole, not about any one value. Runs only after tier 1 passes, so the shape is known.
/// </summary>
internal static class ScenarioDocumentReferentialValidator
{
    private const int Tier = 2;

    public static async Task<List<ScenarioFinding>> RunAsync(
        JsonObject doc, IHttpClientFactory clients, CancellationToken ct)
    {
        var f = new List<ScenarioFinding>();

        var adversaries = Items(doc, "adversaries");
        var edges = Items(doc, "edges");
        var entities = Items(doc, "entities");
        var events = Items(Obj(doc, "timeline"), "events");
        var objectives = Items(Obj(doc, "assessment"), "objectives");
        var references = Items(doc, "references");
        var sources = Items(doc, "sources");
        var workflows = Items(doc, "workflows");
        var terrain = Obj(doc, "terrain");
        var rop = Obj(doc, "rulesOfPlay");

        Identity(f, entities, edges, events, adversaries, objectives, sources, references);
        Objectives(f, events, objectives, adversaries);
        Flags(f, doc, events, adversaries);
        await Workflows(f, events, workflows, clients, ct);
        Techniques(f, adversaries);
        Terrain(f, terrain, events);
        Time(f, rop, events, objectives, adversaries);
        Citations(f, doc, references);

        return f;
    }

    // ───────── (a) ids: edge endpoints exist, and one id means one thing ─────────

    private static void Identity(
        List<ScenarioFinding> f,
        List<JsonNode> entities, List<JsonNode> edges, List<JsonNode> events,
        List<JsonNode> adversaries, List<JsonNode> objectives, List<JsonNode> sources, List<JsonNode> references)
    {
        var entityIds = entities.Select(e => Str(e, "id")).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < edges.Count; i++)
        {
            foreach (var end in new[] { "from", "to" })
            {
                var id = Str(edges[i], end);
                if (id.Length == 0 || entityIds.Contains(id)) continue;
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_ENTITY", $"/edges/{i}/{end}",
                    $"Edge endpoint \"{id}\" is not an entity in this document.",
                    entityIds.Count == 0
                        ? "The document has no entities; an edge needs two."
                        : $"Declared entity ids: {Sample(entityIds)}."));
            }
        }

        // One namespace for the slug ids, because a document that calls two different things by the
        // same name cannot be read out loud. Objective ids are integers and cannot collide with a slug,
        // so they are their own namespace.
        var claimed = new List<(string Id, string Path)>();
        claimed.AddRange(Named(entities, "/entities"));
        claimed.AddRange(Named(events, "/timeline/events"));
        claimed.AddRange(Named(sources, "/sources"));
        claimed.AddRange(Named(references, "/references"));
        var adversaryPaths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < adversaries.Count; i++)
        {
            var id = Str(adversaries[i], "id");
            if (id.Length > 0)
            {
                claimed.Add((id, $"/adversaries/{i}/id"));
                adversaryPaths.Add($"/adversaries/{i}/id");
            }

            claimed.AddRange(Named(Items(adversaries[i] as JsonObject, "playbook"), $"/adversaries/{i}/playbook"));
        }

        // An adversary and the ThreatActor entity it plays are one actor, and sharing the slug is the
        // only way a document binds them: there is no entityRef on an adversary. So one adversary may
        // take a ThreatActor's id; a second one taking it is still two things with one name.
        var threatActors = entities
            .Where(e => string.Equals(Str(e, "type"), "ThreatActor", StringComparison.Ordinal))
            .Select(e => Str(e, "id")).ToHashSet(StringComparer.Ordinal);

        foreach (var group in claimed.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var exempt = threatActors.Contains(group.Key)
                ? group.FirstOrDefault(c => adversaryPaths.Contains(c.Path)).Path
                : null;
            var kept = group.Where(c => c.Path != exempt).ToList();
            if (kept.Count < 2) continue;

            var first = kept[0].Path;
            foreach (var (_, path) in kept.Skip(1))
            {
                f.Add(ScenarioFinding.Err(Tier, "ID_DUPLICATE", path,
                    $"Id \"{group.Key}\" is already used at {first}.",
                    "Ids name one thing each across entities, events, adversaries, moves, sources and references."));
            }
        }

        foreach (var group in objectives.Select((o, i) => (Id: Int(o, "id"), Path: $"/assessment/objectives/{i}/id"))
                     .GroupBy(o => o.Id).Where(g => g.Count() > 1))
        {
            var first = group.First().Path;
            foreach (var (_, path) in group.Skip(1))
            {
                f.Add(ScenarioFinding.Err(Tier, "ID_DUPLICATE", path,
                    $"Objective id {group.Key} is already used at {first}.",
                    "Events and conditions refer to an objective by this number."));
            }
        }
    }

    private static IEnumerable<(string Id, string Path)> Named(List<JsonNode> items, string prefix) =>
        items.Select((n, i) => (Id: Str(n, "id"), Path: $"{prefix}/{i}/id")).Where(x => x.Id.Length > 0);

    // ───────── (b) every objective referred to is an objective declared ─────────

    private static void Objectives(
        List<ScenarioFinding> f, List<JsonNode> events, List<JsonNode> objectives, List<JsonNode> adversaries)
    {
        var declared = objectives.Select(o => Int(o, "id")).Where(id => id > 0).ToHashSet();
        var known = declared.Count == 0
            ? "The document declares no objectives."
            : $"Declared objectives: {string.Join(", ", declared.OrderBy(x => x))}.";

        for (var i = 0; i < events.Count; i++)
        {
            var refs = (events[i] as JsonObject)?["objectives"] as JsonArray ?? [];
            for (var j = 0; j < refs.Count; j++)
            {
                var id = refs[j] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
                if (declared.Contains(id)) continue;
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_OBJECTIVE", $"/timeline/events/{i}/objectives/{j}",
                    $"Event exercises objective {id}, which this document does not declare.", known));
            }
        }

        for (var i = 0; i < objectives.Count; i++)
        {
            if ((objectives[i] as JsonObject)?["parentId"] == null) continue;
            var parent = Int(objectives[i], "parentId");
            if (parent == Int(objectives[i], "id"))
            {
                f.Add(ScenarioFinding.Err(Tier, "REF_OBJECTIVE_SELF_PARENT", $"/assessment/objectives/{i}/parentId",
                    $"Objective {parent} is its own parent."));
            }
            else if (!declared.Contains(parent))
            {
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_OBJECTIVE", $"/assessment/objectives/{i}/parentId",
                    $"Parent objective {parent} is not declared in this document.", known));
            }
        }

        foreach (var (path, text) in Conditions(events, objectives, adversaries))
        {
            foreach (var term in Terms(text).Where(t => t.StartsWith("objective:", StringComparison.Ordinal)))
            {
                var id = int.Parse(term["objective:".Length..], CultureInfo.InvariantCulture);
                if (declared.Contains(id)) continue;
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_OBJECTIVE", path,
                    $"Condition waits on objective {id}, which this document does not declare.",
                    known + " An unknown term makes the whole condition false, so this never fires."));
            }
        }
    }

    // ───────── (c) every flag read is set somewhere; a flag set and never read is dead ─────────

    private static void Flags(List<ScenarioFinding> f, JsonObject doc, List<JsonNode> events, List<JsonNode> adversaries)
    {
        var objectives = Items(Obj(doc, "assessment"), "objectives");
        var set = new Dictionary<string, string>(StringComparer.Ordinal);

        void Sets(JsonArray flags, string prefix)
        {
            for (var i = 0; i < (flags?.Count ?? 0); i++)
            {
                if (flags[i] is JsonValue v && v.TryGetValue<string>(out var name) && name?.Length > 0)
                    set.TryAdd(name, $"{prefix}/{i}");
            }
        }

        Sets(Obj(doc, "startingConditions")?["flags"] as JsonArray, "/startingConditions/flags");
        for (var i = 0; i < events.Count; i++)
        {
            Sets(Obj(events[i] as JsonObject, "effects")?["setFlags"] as JsonArray, $"/timeline/events/{i}/effects/setFlags");
        }
        for (var i = 0; i < adversaries.Count; i++)
        {
            var moves = Items(adversaries[i] as JsonObject, "playbook");
            for (var j = 0; j < moves.Count; j++)
            {
                Sets(Obj(moves[j] as JsonObject, "effects")?["setFlags"] as JsonArray,
                    $"/adversaries/{i}/playbook/{j}/effects/setFlags");
            }
        }

        var read = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, text) in Conditions(events, objectives, adversaries))
        {
            foreach (var term in Terms(text))
            {
                var name = term.StartsWith("flag:", StringComparison.Ordinal) ? term["flag:".Length..]
                    : term.StartsWith('!') ? term[1..]
                    : null;
                if (name == null) continue;
                read.Add(name);
                if (set.ContainsKey(name)) continue;
                // A warning, not an error, and the reason is in the schema: an unset flag makes the
                // condition false rather than malformed, and a branch gated on an outcome the white
                // cell adjudicates has nowhere in this grammar to declare who raises the flag. Three
                // of the four examples read a flag this way on purpose. A typo looks the same, hence
                // the finding.
                f.Add(ScenarioFinding.Warn(Tier, "REF_UNSET_FLAG", path,
                    $"Condition reads flag \"{name}\", which nothing in this document sets, so it is " +
                    "false until something outside the document raises the flag.",
                    "If play is meant to set it, set it in the effects of the event or move that causes " +
                    "it, or in startingConditions.flags; if not, the white cell raises it at run time."));
            }
        }

        // A flag set and never read is not wrong, but it is almost always a branch someone meant to
        // write and did not, so it is said out loud and blocks nothing.
        foreach (var (name, path) in set.Where(kv => !read.Contains(kv.Key)).OrderBy(kv => kv.Value, StringComparer.Ordinal))
        {
            f.Add(ScenarioFinding.Warn(Tier, "FLAG_NEVER_READ", path,
                $"Flag \"{name}\" is set but no condition reads it.",
                "Either a condition is missing, or the flag is left over from an earlier draft."));
        }
    }

    // ───────── (d) workflows: named in this document, and registered in n8n if n8n is there ─────────

    private static async Task Workflows(
        List<ScenarioFinding> f, List<JsonNode> events, List<JsonNode> workflows,
        IHttpClientFactory clients, CancellationToken ct)
    {
        var declared = workflows.Select(w => Str(w, "ref")).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < events.Count; i++)
        {
            var reference = Str(Obj(events[i] as JsonObject, "execution"), "workflowRef");
            if (reference.Length == 0 || declared.Contains(reference)) continue;
            f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_WORKFLOW", $"/timeline/events/{i}/execution/workflowRef",
                $"Event runs workflow \"{reference}\", which this document's workflows list does not declare.",
                declared.Count == 0
                    ? "Nothing is bound unless it is listed in workflows[]."
                    : $"Declared refs: {Sample(declared)}."));
        }

        if (declared.Count == 0) return;

        var registered = await RegisteredWorkflows(clients, ct);
        if (registered == null)
        {
            // Never a pass: the exercise developer has to know this was not checked.
            f.Add(ScenarioFinding.Note(Tier, "WORKFLOW_CHECK_SKIPPED", "/workflows",
                "n8n was not reachable, so no workflow ref was checked against a registered webhook path.",
                "Set N8N_API_URL and N8N_API_KEY to have this checked."));
            return;
        }

        for (var i = 0; i < workflows.Count; i++)
        {
            var reference = Str(workflows[i], "ref");
            if (reference.Length == 0 || registered.Contains(reference)) continue;
            // A warning, not an error: a scenario is written before its automation is deployed, and
            // whether the workflow is live on the day is tier 5's question, not this one's.
            f.Add(ScenarioFinding.Warn(Tier, "WORKFLOW_NOT_REGISTERED", $"/workflows/{i}/ref",
                $"No workflow registered in n8n has the webhook path or name \"{reference}\".",
                "The binding will not fire until a workflow answers on that path."));
        }
    }

    /// <summary>Every name and webhook path n8n knows, or null when n8n could not be asked.</summary>
    private static async Task<HashSet<string>> RegisteredWorkflows(IHttpClientFactory clients, CancellationToken ct)
    {
        var apiUrl = N8nConfig.GetApiUrl();
        var apiKey = N8nConfig.GetApiKey();
        if (clients == null || string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            using var http = clients.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.Add("accept", "application/json");
            http.DefaultRequestHeaders.Add("X-N8N-API-KEY", apiKey);

            using var response = await http.GetAsync(apiUrl, ct);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var list = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
            if (!list.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var workflow in data.EnumerateArray())
            {
                if (workflow.TryGetProperty("name", out var name) && name.GetString() is { } n) names.Add(n);
                if (!workflow.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) continue;
                foreach (var node in nodes.EnumerateArray())
                {
                    if (!node.TryGetProperty("type", out var type) || type.GetString() != "n8n-nodes-base.webhook") continue;
                    if (node.TryGetProperty("parameters", out var parameters)
                        && parameters.TryGetProperty("path", out var path)
                        && path.GetString()?.Trim().TrimStart('/') is { Length: > 0 } p) names.Add(p);
                }
            }
            return names;
        }
        catch (Exception)
        {
            // Unreachable, unauthorized, malformed: all the same answer, which is "not checked".
            return null;
        }
    }

    // ───────── (e) ATT&CK ids MITRE still recognises ─────────

    private static void Techniques(List<ScenarioFinding> f, List<JsonNode> adversaries)
    {
        for (var i = 0; i < adversaries.Count; i++)
        {
            Check(adversaries[i] as JsonObject, $"/adversaries/{i}");
            var moves = Items(adversaries[i] as JsonObject, "playbook");
            for (var j = 0; j < moves.Count; j++) Check(moves[j] as JsonObject, $"/adversaries/{i}/playbook/{j}");
        }

        void Check(JsonObject holder, string prefix)
        {
            var ids = holder?["techniques"] as JsonArray ?? [];
            for (var k = 0; k < ids.Count; k++)
            {
                if (ids[k] is not JsonValue v || !v.TryGetValue<string>(out var id) || string.IsNullOrEmpty(id)) continue;
                var path = $"{prefix}/techniques/{k}";
                var technique = AttackIndex.Find(id);
                if (technique == null)
                {
                    f.Add(ScenarioFinding.Err(Tier, "ATTACK_UNKNOWN_TECHNIQUE", path,
                        $"{id} is not an ATT&CK technique id in Enterprise, ICS or Mobile.",
                        $"Checked against {AttackIndex.Provenance}."));
                }
                else if (technique.Revoked)
                {
                    // MITRE replaced it. An exercise written against a revoked id trains against
                    // something the standard no longer says.
                    f.Add(ScenarioFinding.Err(Tier, "ATTACK_REVOKED_TECHNIQUE", path,
                        $"{id} ({technique.Name}) has been revoked by MITRE.",
                        "Find the technique that replaced it in the current ATT&CK matrix."));
                }
                else if (technique.Deprecated)
                {
                    f.Add(ScenarioFinding.Warn(Tier, "ATTACK_DEPRECATED_TECHNIQUE", path,
                        $"{id} ({technique.Name}) is deprecated in ATT&CK.",
                        "It still means what it meant, but new authoring should not use it."));
                }
            }
        }
    }

    // ───────── (f) terrain: hosts and segments the document declares ─────────

    private static void Terrain(List<ScenarioFinding> f, JsonObject terrain, List<JsonNode> events)
    {
        var hosts = Items(terrain, "hosts");
        var vulnerabilities = Items(terrain, "vulnerabilities");
        var defenses = Items(terrain, "defenses");

        if (hosts.Count == 0)
        {
            var summary = Obj(terrain, "summary");
            var hasProse = summary != null && summary.Any(kv =>
                kv.Value is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s));
            if (hasProse)
            {
                f.Add(ScenarioFinding.Note(Tier, "TERRAIN_UNSTRUCTURED", "/terrain/summary",
                    "The terrain is prose, so host and segment names were not checked.",
                    "Fill terrain.hosts and terrain.segments to have them checked."));
            }
            return;
        }

        var hostNames = hosts.Select(h => Str(h, "name")).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var segmentNames = Items(terrain, "segments").Select(s => Str(s, "name")).Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < hosts.Count; i++)
        {
            var segment = Str(hosts[i], "segment");
            if (segment.Length == 0 || segmentNames.Contains(segment)) continue;
            f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_SEGMENT", $"/terrain/hosts/{i}/segment",
                $"Host sits on segment \"{segment}\", which this document does not declare.",
                segmentNames.Count == 0 ? "The document declares no segments." : $"Declared segments: {Sample(segmentNames)}."));
        }

        for (var i = 0; i < vulnerabilities.Count; i++)
        {
            var asset = Str(vulnerabilities[i], "asset");
            if (asset.Length == 0 || hostNames.Contains(asset)) continue;
            f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_HOST", $"/terrain/vulnerabilities/{i}/asset",
                $"Vulnerability is on \"{asset}\", which is not a host in this document.",
                $"Declared hosts: {Sample(hostNames)}."));
        }

        for (var i = 0; i < defenses.Count; i++)
        {
            var covers = (defenses[i] as JsonObject)?["covers"] as JsonArray ?? [];
            for (var j = 0; j < covers.Count; j++)
            {
                if (covers[j] is not JsonValue v || !v.TryGetValue<string>(out var host) || string.IsNullOrEmpty(host)) continue;
                if (hostNames.Contains(host)) continue;
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_HOST", $"/terrain/defenses/{i}/covers/{j}",
                    $"Defense covers \"{host}\", which is not a host in this document.",
                    $"Declared hosts: {Sample(hostNames)}."));
            }
        }

        // A description that names MER-WEB-07 when the document declares MER-WEB-01 and MER-WEB-02 is
        // almost always a typo, and the event would point at nothing. Only names shaped like a declared
        // host are considered, and only as a warning: prose is prose, and the author may mean a machine
        // outside the exercise.
        var shapes = hostNames.Select(Shape).Where(p => p != null).Distinct(StringComparer.Ordinal).ToList();
        if (shapes.Count == 0) return;
        var pattern = new Regex($"\\b({string.Join("|", shapes)})\\b", RegexOptions.IgnoreCase);

        for (var i = 0; i < events.Count; i++)
        {
            var description = Str(events[i], "description");
            if (description.Length == 0) continue;
            foreach (var mentioned in pattern.Matches(description).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (hostNames.Contains(mentioned)) continue;
                f.Add(ScenarioFinding.Warn(Tier, "REF_UNKNOWN_HOST", $"/timeline/events/{i}/description",
                    $"The description names \"{mentioned}\", which looks like a host in this document but is not one.",
                    $"Declared hosts: {Sample(hostNames)}."));
            }
        }
    }

    /// <summary>A host name with its digit runs loosened: "MER-WEB-01" matches "MER-WEB-07" too.</summary>
    private static string Shape(string name)
    {
        if (!Regex.IsMatch(name, "[0-9]")) return null;
        return Regex.Replace(Regex.Escape(name), "[0-9]+", "[0-9]+");
    }

    // ───────── (g, i) time: inside the duration, and a duration the columns can hold ─────────

    private static void Time(
        List<ScenarioFinding> f, JsonObject rop,
        List<JsonNode> events, List<JsonNode> objectives, List<JsonNode> adversaries)
    {
        var stated = Str(rop, "duration");
        var known = ScenarioDocumentMapper.TryDurationMinutes(stated, out var span);

        if (known && span <= 0)
        {
            f.Add(ScenarioFinding.Err(Tier, "TIME_DURATION_NOT_POSITIVE", "/rulesOfPlay/duration",
                $"The exercise lasts \"{stated}\", which is no time at all."));
        }
        if (known && span > 0 && span % 60 != 0)
        {
            // The two columns that hold this are integer hours. Rounding would lose the minutes and
            // say nothing, so the import refuses instead.
            f.Add(ScenarioFinding.Err(Tier, "TIME_DURATION_NOT_STORABLE", "/rulesOfPlay/duration",
                $"A duration of \"{stated}\" ({span} minutes) cannot be stored: GHOSTS keeps the duration in whole hours, " +
                $"so the {span % 60} minutes past {span / 60}h would be lost.",
                $"Use \"{span / 60}h\" or \"{span / 60 + 1}h\"."));
        }

        var offsets = new List<(string Path, string Value)>();
        if (Str(Obj(rop, "deadline"), "at") is { Length: > 0 } deadline)
            offsets.Add(("/rulesOfPlay/deadline/at", deadline));
        for (var i = 0; i < events.Count; i++)
        {
            if (Str(events[i], "at") is { Length: > 0 } at) offsets.Add(($"/timeline/events/{i}/at", at));
        }

        var clocks = new List<(string Path, int Minutes)>();
        foreach (var (path, text) in Conditions(events, objectives, adversaries))
        {
            foreach (var term in Terms(text).Where(t => t.StartsWith("clock>=", StringComparison.Ordinal)))
            {
                if (int.TryParse(term["clock>=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                    clocks.Add((path, minutes));
            }
        }

        if (!known || span <= 0)
        {
            if (offsets.Count > 0 || clocks.Count > 0)
            {
                f.Add(ScenarioFinding.Note(Tier, "TIME_NO_DURATION", "/rulesOfPlay",
                    $"The document states no length of play, so {offsets.Count + clocks.Count} time reference(s) were not checked against it.",
                    "Set rulesOfPlay.duration."));
            }
            return;
        }

        foreach (var (path, value) in offsets)
        {
            if (!ScenarioDocumentMapper.TryOffsetMinutes(value, out var minutes) || minutes <= span) continue;
            f.Add(ScenarioFinding.Err(Tier, "TIME_OUTSIDE_DURATION", path,
                $"{value} is {minutes} minutes into an exercise that lasts {stated} ({span} minutes), so it never happens.",
                $"Move it inside {stated}, or lengthen rulesOfPlay.duration."));
        }

        foreach (var (path, minutes) in clocks.Where(c => c.Minutes > span))
        {
            f.Add(ScenarioFinding.Err(Tier, "TIME_OUTSIDE_DURATION", path,
                $"The condition waits for clock>={minutes}, past the end of an exercise that lasts {stated} ({span} minutes), so it never holds.",
                $"Lower the threshold, or lengthen rulesOfPlay.duration."));
        }
    }

    // ───────── (h) [ref:id] citations name a reference ─────────

    private static readonly Regex Citation = new(@"\[ref:([^\]]*)\]", RegexOptions.Compiled);

    private static void Citations(List<ScenarioFinding> f, JsonObject doc, List<JsonNode> references)
    {
        var declared = references.Select(r => Str(r, "id")).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);

        foreach (var (path, text) in Prose(doc, string.Empty))
        {
            foreach (var cited in Citation.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal))
            {
                if (declared.Contains(cited)) continue;
                f.Add(ScenarioFinding.Err(Tier, "REF_UNKNOWN_REFERENCE", path,
                    $"The text cites [ref:{cited}], which this document's references list does not declare.",
                    declared.Count == 0
                        ? "Add the citation to references[] so a reviewer can open it."
                        : $"Declared references: {Sample(declared)}."));
            }
        }
    }

    /// <summary>Every string in the document, with a pointer to it. Citations can be in any prose field.</summary>
    private static IEnumerable<(string Path, string Text)> Prose(JsonNode node, string path)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var kv in o)
                foreach (var found in Prose(kv.Value, $"{path}/{kv.Key.Replace("~", "~0").Replace("/", "~1")}"))
                {
                    yield return found;
                }
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                foreach (var found in Prose(a[i], $"{path}/{i}"))
                {
                    yield return found;
                }
                break;
            case JsonValue v when v.TryGetValue<string>(out var s) && s.Length > 0:
                yield return (path, s);
                break;
        }
    }

    // ───────── shared reading ─────────

    /// <summary>Every condition in the document, with a pointer to it. One place, so no reader is forgotten.</summary>
    private static IEnumerable<(string Path, string Text)> Conditions(
        List<JsonNode> events, List<JsonNode> objectives, List<JsonNode> adversaries)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (Str(events[i], "when") is { Length: > 0 } when) yield return ($"/timeline/events/{i}/when", when);
        }
        for (var i = 0; i < objectives.Count; i++)
        {
            if (Str(objectives[i], "metWhen") is { Length: > 0 } met) yield return ($"/assessment/objectives/{i}/metWhen", met);
        }
        for (var i = 0; i < adversaries.Count; i++)
        {
            var moves = Items(adversaries[i] as JsonObject, "playbook");
            for (var j = 0; j < moves.Count; j++)
            {
                if (Str(moves[j], "preconditions") is { Length: > 0 } pre)
                    yield return ($"/adversaries/{i}/playbook/{j}/preconditions", pre);
            }
        }
    }

    private static IEnumerable<string> Terms(string condition) =>
        condition.Split("&&").Select(t => t.Trim()).Where(t => t.Length > 0);

    private static JsonObject Obj(JsonObject parent, string key) => parent?[key] as JsonObject;

    private static List<JsonNode> Items(JsonObject parent, string key) =>
        parent?[key] is JsonArray a ? a.Where(n => n != null).ToList() : [];

    private static string Str(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s ?? string.Empty : string.Empty;

    private static int Int(JsonNode parent, string key) =>
        (parent as JsonObject)?[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    /// <summary>A few of the declared names, so the hint helps without printing a hundred of them.</summary>
    private static string Sample(IEnumerable<string> names)
    {
        var ordered = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        return ordered.Count <= 8
            ? string.Join(", ", ordered)
            : string.Join(", ", ordered.Take(8)) + $", and {ordered.Count - 8} more";
    }
}
