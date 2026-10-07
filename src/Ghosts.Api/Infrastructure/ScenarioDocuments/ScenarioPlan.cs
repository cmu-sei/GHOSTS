// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// The document as an exercise plan a reviewer works through section by section (B4). A view, never a
/// source: it is rendered from the document by this code, so it cannot drift from what the loader reads,
/// and nothing in it is written by hand or by a model. This is a port of the <c>render</c> command in
/// <c>schemas/scenario-document/tools/scenario-doc.mjs</c>, section for section; the examples' <c>*.plan.md</c>
/// are that tool's output, and a test holds this port to them.
/// </summary>
public static class ScenarioPlan
{
    /// <param name="doc">The scenario document.</param>
    /// <param name="renderedFrom">What the first line says the plan was rendered from, such as a file name or a hash.</param>
    public static string Render(JsonObject doc, string renderedFrom)
    {
        var out_ = new List<string>();

        Heading(out_, 1, $"{S(doc["name"]) ?? S(doc["slug"])} — exercise plan");
        Para(out_, $"*Rendered from {renderedFrom} (schema {S(doc["schemaVersion"])}). A view, not a source: " +
                   "regenerate it after every edit. What gets signed is the plan; what gets loaded is the document.*");
        Para(out_, S(doc["description"]));

        if (Truthy(doc["intent"])) { Heading(out_, 2, "Intent"); Para(out_, S(doc["intent"])); }
        var context = doc["context"];
        if (Truthy(Get(context, "situation"))) { Heading(out_, 2, "The situation the audience is given"); Para(out_, S(Get(context, "situation"))); }
        if (Truthy(Get(context, "political"))) { Heading(out_, 2, "Background (White Cell)"); Para(out_, S(Get(context, "political"))); }

        var a = doc["audience"];
        if (a != null || doc["sides"] != null)
        {
            Heading(out_, 2, "The audience");
            if (Truthy(Get(a, "role")))
                Para(out_, $"**Who.** {S(Get(a, "role"))}{(Truthy(Get(a, "size")) ? $", {S(Get(a, "size"))} of them" : "")}.");
            if (Truthy(Get(a, "proficiency"))) Para(out_, $"**Where they are starting from.** {S(Get(a, "proficiency"))}");
            if (Truthy(Get(a, "mandate"))) Para(out_, $"**What they are responsible for.** {S(Get(a, "mandate"))}");
            if (Truthy(Get(a, "rulesOfEngagement"))) Para(out_, $"**What they may and may not do.** {S(Get(a, "rulesOfEngagement"))}");
            Table(out_, ["Side", "Alignment"], List(doc["sides"]).Select(s => new[] { S(Get(s, "name")), S(Get(s, "alignment")) }));
        }

        var r = doc["rulesOfPlay"];
        if (r != null)
        {
            Heading(out_, 2, "How it is played");
            var clock = Get(r, "clock");
            var deadline = Get(r, "deadline");
            var telemetry = Get(r, "telemetry") as JsonObject;
            string[] Row(string name, string value) => [name, value];
            var rows = new List<string[]>
            {
                Row("Runs for", S(Get(r, "duration"))), Row("Pacing", S(Get(r, "pacing"))), Row("Adjudication", S(Get(r, "adjudication"))),
                Row("Clock tick", Truthy(Get(clock, "tickMinutes"))
                    ? $"{S(Get(clock, "tickMinutes"))} minutes{(Truthy(Get(clock, "label")) ? $" ({S(Get(clock, "label"))})" : "")}" : null),
                Row("Deadline", Truthy(Get(deadline, "at"))
                    ? $"{S(Get(deadline, "at"))}{(Truthy(Get(deadline, "label")) ? $" — {S(Get(deadline, "label"))}" : "")}" : null),
                Row("Fog of war", S(Get(r, "fog"))),
                Row("Telemetry", string.Join(", ", (telemetry ?? []).Where(kv => Truthy(kv.Value)).Select(kv => kv.Key)))
            };
            Table(out_, ["Rule of play", ""], rows.Where(row => !string.IsNullOrEmpty(row[1])));

            var ladder = Get(r, "escalationLadder");
            if (ladder != null)
            {
                Heading(out_, 3, "How far it can go");
                Para(out_, S(Get(ladder, "summary")));
                Table(out_, ["Rung", "What has happened", "Still recoverable"],
                    List(Get(ladder, "rungs")).Select(g => new[] { S(Get(g, "name")), S(Get(g, "description")), Truthy(Get(g, "recoverable")) ? "yes" : "**no**" }));
            }
            if (Truthy(Get(Get(r, "branching"), "summary"))) { Heading(out_, 3, "If play diverges"); Para(out_, S(Get(Get(r, "branching"), "summary"))); }
        }

        var flags = List(Get(doc["startingConditions"], "flags"));
        var facts = (Get(doc["startingConditions"], "facts") as JsonObject ?? []).ToList();
        if (flags.Count > 0 || facts.Count > 0)
        {
            Heading(out_, 2, "What is true when it starts");
            if (flags.Count > 0) Para(out_, string.Join(", ", flags.Select(f => Em(S(f)))));
            Table(out_, ["Fact", "Value"], facts.Select(kv => new[] { kv.Key, S(kv.Value) }));
        }

        RenderAdversaries(out_, doc);
        RenderTerrain(out_, doc);
        RenderEntities(out_, doc);
        RenderTimeline(out_, doc);

        var s = doc["assessment"];
        if (s != null)
        {
            Heading(out_, 2, "What counts as success");
            if (Truthy(Get(s, "purpose"))) Para(out_, $"**Why this exercise exists.** {S(Get(s, "purpose"))}");
            if (Truthy(Get(s, "victoryConditions"))) Para(out_, $"**Victory conditions.** {S(Get(s, "victoryConditions"))}");
            if (Truthy(Get(s, "performanceMetrics"))) Para(out_, $"**How it is measured.** {S(Get(s, "performanceMetrics"))}");
            Table(out_, ["Id", "Objective", "Type", "Priority", "Met when", "Assigned"],
                List(Get(s, "objectives")).Select(o => new[]
                {
                    S(Get(o, "id")),
                    S(Get(o, "name")) + (Truthy(Get(o, "parentId")) ? $" (under {S(Get(o, "parentId"))})" : ""),
                    S(Get(o, "type")), S(Get(o, "priority")),
                    Truthy(Get(o, "metWhen")) ? $"`{S(Get(o, "metWhen"))}`" : S(Get(o, "successCriteria")),
                    S(Get(o, "assigned"))
                }));
        }

        Table(out_, ["Automation", "Bound to"],
            List(doc["workflows"]).Select(w => new[] { Or(S(Get(w, "displayName")), S(Get(w, "ref"))), S(Get(w, "ref")) }));

        var refs = List(doc["references"]);
        if (refs.Count > 0)
        {
            Heading(out_, 2, "References");
            Para(out_, "Every `[ref:id]` in the prose above points at one of these.");
            Table(out_, ["Id", "Title", "Where"],
                refs.Select(x => new[] { S(Get(x, "id")), S(Get(x, "title")), Commas(new[] { S(Get(x, "uri")), S(Get(x, "locator")) }.Where(v => !string.IsNullOrEmpty(v))) }));
        }

        return Regex.Replace(string.Join("\n", out_), "\n{3,}", "\n\n") + "\n";
    }

    private static void RenderTimeline(List<string> out_, JsonObject doc)
    {
        var events = List(Get(doc["timeline"], "events"));
        if (events.Count == 0) return;
        Heading(out_, 2, "Master Scenario Events List");
        Para(out_, "In exercise-planning terms, each row below is an inject: a controller-triggered stimulus " +
                   "with a time or a trigger, an owner, and the response it is meant to provoke.");
        Para(out_, $"{events.Count} beat{(events.Count == 1 ? "" : "s")}. Every one names who owns it, what the " +
                   "audience is expected to do, and what they would have to see in order to do it.");

        Table(out_, ["#", "When", "Owner", "Beat", "Expected response", "Indicators"],
            events.Select((e, i) => new[]
            {
                (i + 1).ToString(), Timing(e), Or(S(Get(e, "owner")), "unassigned"),
                Or(S(Get(e, "title")), Or(S(Get(e, "description")), S(Get(e, "id")))),
                S(Get(e, "expectedResponse")), Commas(List(Get(e, "indicators")).Select(S))
            }));

        // Then each beat in prose, because the fields a table cannot hold are the ones that decide whether
        // the beat is a faithful reading of the intent: what it sets, what it is graded against, and how it
        // is executed.
        Heading(out_, 3, "Beat by beat");
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var effects = Get(e, "effects");
            var setFlags = List(Get(effects, "setFlags"));
            var setFacts = Get(effects, "setFacts") as JsonObject;
            var indicators = List(Get(e, "indicators"));
            var objectives = List(Get(e, "objectives"));

            // The mechanics read as one semicolon-joined clause: "Fires on the clock; sets foothold-billing;
            // observable in the mail gateway logs and one workstation process tree."
            var how = new List<string>();
            if (Truthy(Get(e, "when"))) how.Add($"fires when `{S(Get(e, "when"))}` holds");
            else if (Truthy(Get(e, "schedule"))) how.Add($"fires on the schedule `{S(Get(e, "schedule"))}`");
            else how.Add("fires on the clock");
            if (setFlags.Count > 0) how.Add($"sets {string.Join(", ", setFlags.Select(f => Em(S(f))))}");
            if (setFacts != null) how.Add($"records {string.Join(", ", setFacts.Select(kv => $"{kv.Key} = {S(kv.Value)}"))}");
            if (indicators.Count > 0) how.Add($"observable in {Commas(indicators.Select(S))}");
            if (objectives.Count > 0) how.Add($"graded against objective {Commas(objectives.Select(S))}");
            if (S(Get(Get(e, "execution"), "mode")) == "workflow") how.Add($"run by workflow `{S(Get(Get(e, "execution"), "workflowRef"))}`");

            var parts = new List<string> { $"**{i + 1}. {Or(S(Get(e, "title")), S(Get(e, "id")))} — {Timing(e)}**, {Or(S(Get(e, "owner")), "unassigned")}." };
            if (Truthy(Get(e, "description"))) parts.Add($"{Sentence(S(Get(e, "description")))}.");
            var mechanics = string.Join("; ", how.Select(Sentence));
            parts.Add($"{char.ToUpperInvariant(mechanics[0])}{mechanics[1..]}.");
            if (Truthy(Get(e, "expectedResponse"))) parts.Add($"**Expected response:** {Sentence(S(Get(e, "expectedResponse")))}.");
            out_.Add(string.Join(" ", parts));
            out_.Add("");
        }
    }

    private static void RenderAdversaries(List<string> out_, JsonObject doc)
    {
        var adversaries = List(doc["adversaries"]);
        if (adversaries.Count == 0) return;
        Heading(out_, 2, "The adversary");
        foreach (var a in adversaries)
        {
            Heading(out_, 3, $"{Or(S(Get(a, "name")), S(Get(a, "id")))} — {Or(S(Get(a, "type")), "unspecified")}, capability {S(Get(a, "capability")) ?? "?"}/5");
            if (Truthy(Get(a, "objective"))) Para(out_, $"**What it is trying to do.** {S(Get(a, "objective"))}");
            if (Get(a, "winThreshold") != null) Para(out_, $"**It wins at** progress {S(Get(a, "winThreshold"))}.");
            var techniques = List(Get(a, "techniques"));
            if (techniques.Count > 0) Para(out_, $"**Techniques.** {Commas(techniques.Select(S))} (ATT&CK ids; the validator resolves each one).");
            var capabilities = List(Get(a, "capabilities"));
            if (capabilities.Count > 0) Para(out_, $"**Capabilities in plain words.** {Commas(capabilities.Select(S))}");
            Table(out_, ["Move", "Domain", "Techniques", "Available when", "Sets", "Progress", "Indicators"],
                List(Get(a, "playbook")).Select(m => new[]
                {
                    Or(S(Get(m, "description")), S(Get(m, "id"))), S(Get(m, "domain")), Commas(List(Get(m, "techniques")).Select(S)),
                    Truthy(Get(m, "preconditions")) ? $"`{S(Get(m, "preconditions"))}`" : "always",
                    Commas(List(Get(Get(m, "effects"), "setFlags")).Select(S)), S(Get(m, "progress")),
                    Commas(List(Get(m, "indicators")).Select(S))
                }));
        }
    }

    // A Mermaid identifier has to be word characters only; names and ids in the document do not.
    private static string MermaidId(string s)
    {
        var id = Regex.Replace(Regex.Replace(s ?? "", "[^a-zA-Z0-9_]", "_"), @"^(\d)", "_$1");
        return id.Length == 0 ? "x" : id;
    }

    private static string MermaidLabel(string s) => (s ?? "").Replace("\"", "'");

    // One node per host, grouped in a subgraph per segment, so a reviewer sees the topology before the
    // tables spell it out row by row. Mermaid in a fenced code block renders inline in GitHub and VS Code,
    // and a viewer that does not still shows readable text, one line per host, grouped by segment.
    private static void RenderTopologyFigure(List<string> out_, JsonNode t)
    {
        var hosts = List(Get(t, "hosts"));
        if (hosts.Count == 0) return;
        var segments = List(Get(t, "segments"));
        var bySegment = new List<(string Name, List<JsonNode> Hosts)>();
        var unassigned = new List<JsonNode>();
        foreach (var s in segments) bySegment.Add((S(Get(s, "name")), [])); // declared even if nothing is on it yet
        foreach (var h in hosts)
        {
            var segment = S(Get(h, "segment"));
            if (Truthy(Get(h, "segment")))
            {
                var index = bySegment.FindIndex(x => x.Name == segment);
                if (index < 0) { bySegment.Add((segment, [])); index = bySegment.Count - 1; }
                bySegment[index].Hosts.Add(h);
            }
            else
            {
                unassigned.Add(h);
            }
        }

        Para(out_, "**Network diagram.** One node per host, grouped by segment. If this Markdown viewer does " +
                   "not render Mermaid, the fenced block below is still a complete list, host by host.");
        out_.Add("```mermaid");
        out_.Add("graph LR");
        foreach (var (segName, segHosts) in bySegment)
        {
            var seg = segments.FirstOrDefault(s => S(Get(s, "name")) == segName);
            var label = Truthy(Get(seg, "cidr")) ? $"{segName} ({S(Get(seg, "cidr"))})" : segName;
            out_.Add($"  subgraph {MermaidId(segName)}[\"{MermaidLabel(label)}\"]");
            foreach (var h in segHosts)
            {
                var text = string.Join("<br/>", new[] { S(Get(h, "name")), S(Get(h, "role")) }.Where(v => !string.IsNullOrEmpty(v)));
                out_.Add($"    {MermaidId(S(Get(h, "name")))}[\"{MermaidLabel(text)}\"]");
            }
            out_.Add("  end");
        }
        if (unassigned.Count > 0)
        {
            out_.Add("  subgraph unassigned[\"(segment not stated)\"]");
            foreach (var h in unassigned) out_.Add($"    {MermaidId(S(Get(h, "name")))}[\"{MermaidLabel(S(Get(h, "name")))}\"]");
            out_.Add("  end");
        }
        out_.Add("```");
        out_.Add("");
    }

    private static void RenderTerrain(List<string> out_, JsonObject doc)
    {
        var t = doc["terrain"];
        if (t == null) return;
        Heading(out_, 2, "The terrain");
        var reference = Get(t, "reference");
        if (reference != null)
            Para(out_, $"**Base.** The `{S(Get(reference, "slice"))}` slice from {S(Get(reference, "provider"))}. " +
                       "Everything below is what this exercise adds to it or relies on from it.");
        var summary = Get(t, "summary");
        if (Truthy(Get(summary, "topology"))) Para(out_, $"**Topology.** {S(Get(summary, "topology"))}");
        if (Truthy(Get(summary, "assets"))) Para(out_, $"**Assets.** {S(Get(summary, "assets"))}");
        if (Truthy(Get(summary, "services"))) Para(out_, $"**Services.** {S(Get(summary, "services"))}");
        RenderTopologyFigure(out_, t);
        Table(out_, ["Segment", "CIDR", "What it is"], List(Get(t, "segments")).Select(s => new[] { S(Get(s, "name")), S(Get(s, "cidr")), S(Get(s, "description")) }));
        Table(out_, ["Host", "Segment", "OS", "Role", "What it is"],
            List(Get(t, "hosts")).Select(h => new[] { S(Get(h, "name")), S(Get(h, "segment")), S(Get(h, "os")), S(Get(h, "role")), S(Get(h, "description")) }));
        Table(out_, ["Service", "On", "What it is"], List(Get(t, "services")).Select(s => new[] { S(Get(s, "name")), Commas(List(Get(s, "hosts")).Select(S)), S(Get(s, "description")) }));
        Table(out_, ["Defense", "Covers", "What it does"],
            List(Get(t, "defenses")).Select(d => new[] { S(Get(d, "name")), Commas(List(Get(d, "covers")).Select(S)), S(Get(d, "description")) }));
        Table(out_, ["Weakness", "On", "Severity", "What it is"],
            List(Get(t, "vulnerabilities")).Select(v => new[]
            {
                Or(S(Get(v, "cve")), S(Get(v, "description"))), S(Get(v, "asset")), S(Get(v, "severity")),
                Truthy(Get(v, "cve")) ? S(Get(v, "description")) : ""
            }));
        var info = Get(t, "informationEnvironment");
        if (info != null)
            Para(out_, $"**Information environment.** {Commas(List(Get(info, "platforms")).Select(S))}" +
                       (Truthy(Get(info, "audience")) ? $" — {S(Get(info, "audience"))}" : ""));
        var pools = List(Get(doc["population"], "pools"));
        if (pools.Count > 0)
        {
            Heading(out_, 3, "Who GHOSTS simulates");
            Para(out_, $"{pools.Sum(p => Get(p, "count") is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0)} simulated people. Everyone else in the " +
                       "exercise is a person playing.");
            Table(out_, ["Pool", "Count", "Who they are"], pools.Select(p => new[] { S(Get(p, "role")), S(Get(p, "count")), S(Get(p, "description")) }));
        }
    }

    // Every entity the document names, with its type, description and provenance. Provenance follows the
    // schema's own defaults (v1.1.0 rule 9) when the document omits it: operator origin, confidence 1, not
    // reviewed, and the plan says so rather than showing a blank.
    private static void RenderEntities(List<string> out_, JsonObject doc)
    {
        var entities = List(doc["entities"]);
        if (entities.Count == 0) return;
        Heading(out_, 2, "Entities");
        Para(out_, "Everyone and everything the document names as part of the graph — people, systems, " +
                   "organizations, and the rest of the recommended vocabulary — so nothing here is absent from the plan.");
        Table(out_, ["Id", "Name", "Type", "Description", "Provenance"],
            entities.Select(e =>
            {
                var p = Get(e, "provenance");
                var origin = S(Get(p, "origin")) ?? "operator";
                var confidence = S(Get(p, "confidence")) ?? "1";
                var reviewed = Truthy(Get(p, "reviewed")) ? "reviewed" : "not reviewed";
                return new[] { S(Get(e, "id")), S(Get(e, "name")), S(Get(e, "type")), S(Get(e, "description")), $"{origin}, confidence {confidence}, {reviewed}" };
            }));
    }

    // When an event happens, in the words the document used. A condition is not a time and must not be
    // printed as one: "when the foothold is held" is a different claim from "at 09:40".
    private static string Timing(JsonNode e)
    {
        var parts = new List<string>();
        if (Truthy(Get(e, "displayTime"))) parts.Add(S(Get(e, "displayTime")) + (Truthy(Get(e, "at")) ? $" ({S(Get(e, "at"))})" : ""));
        else if (Truthy(Get(e, "at"))) parts.Add(S(Get(e, "at")));
        if (Truthy(Get(e, "schedule"))) parts.Add($"on schedule `{S(Get(e, "schedule"))}`");
        if (Truthy(Get(e, "when"))) parts.Add($"when `{S(Get(e, "when"))}`");
        return parts.Count > 0 ? string.Join(", ", parts) : "unscheduled";
    }

    // A table, or nothing at all when there are no rows: an empty table is a question a reviewer has to
    // stop and answer ("is this blank because there is nothing, or because something failed?").
    private static void Table(List<string> out_, string[] headers, IEnumerable<string[]> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        out_.Add($"| {string.Join(" | ", headers)} |");
        out_.Add($"|{string.Concat(headers.Select(_ => "---|"))}");
        foreach (var row in list) out_.Add($"| {string.Join(" | ", row.Select(Cell))} |");
        out_.Add("");
    }

    private static void Heading(List<string> out_, int level, string text) { out_.Add($"{new string('#', level)} {text}"); out_.Add(""); }

    private static void Para(List<string> out_, string text) { if (!string.IsNullOrEmpty(text)) { out_.Add(text); out_.Add(""); } }

    private static string Cell(string v)
    {
        var text = Regex.Replace((v ?? "").Replace("|", "\\|"), "\n+", " ").Trim();
        return text.Length == 0 ? "—" : text;
    }

    private static string Sentence(string s) => Regex.Replace((s ?? "").Trim(), @"\.$", "");
    private static string Em(string v) => string.IsNullOrEmpty(v) ? "" : $"*{v}*";
    private static string Commas(IEnumerable<string> values) => string.Join(", ", values);
    private static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b : a;

    /// <summary>A child of an object, or null when the node is not an object or has no such key.</summary>
    private static JsonNode Get(JsonNode node, string key) => node is JsonObject o ? o[key] : null;

    /// <summary>An array's items; a missing value is no items; any other value is one item.</summary>
    private static List<JsonNode> List(JsonNode node) => node switch
    {
        JsonArray array => array.ToList(),
        null => [],
        _ => [node]
    };

    /// <summary>The value as the tool's String(v) writes it: text as is, numbers and booleans as JSON, null as null.</summary>
    private static string S(JsonNode node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v => v.ToJsonString(),
        _ => node.ToJsonString()
    };

    /// <summary>JavaScript truthiness for the values a document holds: absent, empty text, 0 and false are false.</summary>
    private static bool Truthy(JsonNode node) => node switch
    {
        null => false,
        JsonValue v when v.TryGetValue<string>(out var s) => s.Length > 0,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<double>(out var d) => !double.IsNaN(d) && System.Math.Abs(d) > double.Epsilon,
        _ => true
    };
}
