// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ScenarioBuilder.Api.Domain;

/// <summary>
/// The whole scenario as one readable Markdown file: what the JSON export carries, stage by stage in the kind's
/// order, each answer with who gave it and why, each draft after the stage it's due, then the open findings.
/// </summary>
public static partial class ScenarioMarkdown
{
    public static string Build(Kind kind, Scenario scenario, IReadOnlyList<Decision> decisions, IReadOnlyList<Approval> approvals)
    {
        var findings = Ledger.Progress(kind, scenario, decisions, approvals).Gate.Findings;
        var byQuestion = decisions.ToDictionary(d => d.QuestionId);
        var drafts = Ledger.ReadDrafts(scenario);
        var md = new StringBuilder();

        if (findings.Count > 0)
            md.Append($"> Draft: {findings.Count} {(findings.Count == 1 ? "finding" : "findings")} open.\n\n");
        md.Append($"# {scenario.Name}\n\n{kind.Name}, kind version {scenario.KindVersion}. Open stage: {scenario.Stage}.\n");

        foreach (var stage in kind.Stages)
        {
            md.Append($"\n## Stage {stage.Number}: {stage.Title}\n\n");
            foreach (var q in stage.Questions)
            {
                var label = q.Label ?? q.Id;
                if (!byQuestion.TryGetValue(q.Id, out var decision))
                {
                    md.Append($"- **{label}:** _Not answered yet._\n");
                    continue;
                }
                if (JsonNode.Parse(decision.Value) is JsonArray list)
                {
                    md.Append($"- **{label}:**\n");
                    foreach (var item in list)
                        md.Append($"  - {Indent(item?.GetValue<string>() ?? "", "    ")}\n");
                }
                else
                    md.Append($"- **{label}:** {Indent(JsonNode.Parse(decision.Value)?.GetValue<string>() ?? "", "  ")}\n");
                md.Append($"  _{Provenance(decision)}_\n");
            }

            foreach (var draft in kind.Drafts.Where(d => d.DueAfterStage == stage.Number))
                if (drafts[draft.Field] is JsonValue text && !string.IsNullOrWhiteSpace(text.GetValue<string>()))
                    md.Append($"\n### {Spaced(draft.Field)}\n\n{text.GetValue<string>().Trim()}\n");
        }

        if (findings.Count > 0)
        {
            md.Append("\n## Open Findings\n\n");
            foreach (var f in findings)
                md.Append($"- Stage {f.Stage}: {f.Message}\n");
        }
        return md.ToString();
    }

    private static string Provenance(Decision d)
    {
        var who = d.Who == Who.Proposed
            ? $"Proposed by {(d.Via == Via.Agent ? "the agent" : d.DecidedBy)}."
            : $"Stated by {d.DecidedBy}.";
        return d.Reason is null ? who : $"{who} {d.Reason}";
    }

    /// <summary>Keeps a multi-line answer inside its list item.</summary>
    private static string Indent(string text, string by) => text.Trim().Replace("\r\n", "\n").Replace("\n", "\n" + by);

    private static readonly HashSet<string> Minor = ["a", "an", "and", "as", "at", "but", "by", "for", "in", "nor", "of", "on", "or", "the", "to", "vs"];

    /// <summary>executiveSummary → Executive Summary: title case, the joining words inside it kept lower case.</summary>
    private static string Spaced(string id)
    {
        var words = Capital().Replace(id, " $0").ToLowerInvariant().Split(' ');
        return string.Join(' ', words.Select((w, i) => i > 0 && i < words.Length - 1 && Minor.Contains(w) ? w : char.ToUpperInvariant(w[0]) + w[1..]));
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex Capital();
}
