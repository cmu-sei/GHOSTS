// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

/// <summary>The rules of the question ledger, as pure functions over a kind and a scenario's rows.</summary>
public static class Ledger
{
    /// <summary>How answers are stored: readable text, not \u escapes.</summary>
    public static readonly JsonSerializerOptions Storage = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Null when <paramref name="value"/> fits the question; otherwise what it takes, worded for the agent.</summary>
    public static string? ValidateValue(KindQuestion q, JsonNode? value, out JsonNode? canonical)
    {
        canonical = null;
        switch (q.Answer)
        {
            case AnswerType.Text:
                if (AsString(value) is not { } text || string.IsNullOrWhiteSpace(text))
                    return $"'{q.Id}' takes non-empty text.";
                canonical = JsonValue.Create(text);
                return null;

            case AnswerType.Choice:
                var picked = AsString(value)?.Trim();
                var option = q.Options!.FirstOrDefault(o => string.Equals(o, picked, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                    return $"'{q.Id}' takes one of: {string.Join(", ", q.Options!)}.";
                canonical = JsonValue.Create(option);
                return null;

            case AnswerType.List:
                var items = value is JsonArray array ? array.Select(AsString).ToList() : null;
                if (items is not { Count: > 0 } || items.Any(string.IsNullOrWhiteSpace))
                    return $"'{q.Id}' takes a list of strings, like [\"{ListExample(q)}\"].";
                canonical = new JsonArray(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray());
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(q), q.Answer, null);
        }
    }

    /// <summary>
    /// The open stage and its gate. The gate passes when every question is answered, every draft due after the stage is
    /// written, and, when the stage names an approval, the owner's latest approval still matches the stage's content.
    /// A closed stage's approval is checked again too: a change to what it covered stops the gate until the owner
    /// approves that stage again.
    /// </summary>
    public static ProgressView Progress(Kind kind, Scenario scenario, IReadOnlyList<Decision> decisions, IReadOnlyList<Approval> approvals)
    {
        var stage = kind.Stages[scenario.Stage];
        var answered = decisions.ToDictionary(d => d.QuestionId);
        // Every stage up to the open one: a question the kind gained after its stage closed is asked like any other.
        var open = kind.Stages.Take(scenario.Stage + 1)
            .SelectMany(s => s.Questions.Where(q => !answered.ContainsKey(q.Id)).Select(q => QuestionView.From(q, s.Number))).ToList();
        var findings = open.Select(q => new Finding("stop", "QUESTION_OPEN", $"'{q.Id}' is not answered.", q.Stage)).ToList();

        var drafts = ReadDrafts(scenario);
        List<string> draftsDue = open.All(q => q.Stage != stage.Number)
            ? kind.Drafts.Where(d => d.DueAfterStage == stage.Number && IsBlank(drafts[d.Field])).Select(d => d.Field).ToList()
            : [];
        findings.AddRange(draftsDue.Select(f => new Finding("stop", "DRAFT_MISSING", $"The {f} draft isn't written yet.", stage.Number)));

        var approved = false;
        if (stage.Gate.Approval is { } approval)
        {
            approved = Approved(kind, stage.Number, decisions, approvals, drafts, out var everApproved);
            // The approval is the last step: ask for it only once nothing else in this stage is in the way.
            if (!approved && findings.All(f => f.Stage != stage.Number))
                findings.Add(everApproved
                    ? new Finding("stop", "APPROVAL_STALE", $"{approval}: what was approved has changed since. The owner must approve again.", stage.Number)
                    : new Finding("stop", "APPROVAL_MISSING", $"Waiting for the owner: {approval}.", stage.Number));
        }

        for (var closed = 0; closed < scenario.Stage; closed++)
            if (kind.Stages[closed].Gate.Approval is { } closedApproval && open.All(q => q.Stage != closed)
                && !Approved(kind, closed, decisions, approvals, drafts, out _))
                findings.Add(new Finding("stop", "APPROVAL_STALE",
                    $"Stage {closed}: {closedApproval}: what was approved has changed since. The owner must approve stage {closed} again.", closed));

        return new ProgressView(
            kind.Id,
            scenario.Stage,
            stage.Title,
            scenario.Stage == kind.LastStage,
            new GateView(findings.Count == 0, stage.Gate.Approval, approved, findings),
            open,
            kind.AllQuestions.Where(q => answered.ContainsKey(q.Id)).Select(q => AnsweredView.From(answered[q.Id])).ToList(),
            draftsDue,
            scenario.Delegated);
    }

    /// <summary>What an approval of <paramref name="stage"/> covers: its answers in kind order, then the drafts due after it.</summary>
    public static string StageHash(Kind kind, int stage, IReadOnlyList<Decision> decisions, JsonObject drafts)
    {
        var values = decisions.ToDictionary(d => d.QuestionId, d => d.Value);
        var content = new JsonArray();
        foreach (var q in kind.Stages[stage].Questions)
            content.Add(values.TryGetValue(q.Id, out var value) ? JsonNode.Parse(value) : null);
        foreach (var draft in kind.Drafts.Where(d => d.DueAfterStage == stage))
            content.Add(drafts[draft.Field]?.DeepClone());
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToJsonString(Storage))));
    }

    public static JsonObject ReadDrafts(Scenario scenario) => JsonNode.Parse(scenario.Drafts) as JsonObject ?? [];

    /// <summary>Whether the owner's latest approval of <paramref name="stage"/> still matches what the stage holds.</summary>
    private static bool Approved(Kind kind, int stage, IReadOnlyList<Decision> decisions, IReadOnlyList<Approval> approvals,
        JsonObject drafts, out bool everApproved)
    {
        var latest = approvals.LastOrDefault(a => a.Stage == stage);
        everApproved = latest is not null;
        return latest is not null && latest.Hash == StageHash(kind, stage, decisions, drafts);
    }

    public static bool StageAnswered(Kind kind, int stage, IReadOnlyList<Decision> decisions) =>
        kind.Stages[stage].Questions.All(q => decisions.Any(d => d.QuestionId == q.Id));

    private static bool IsBlank(JsonNode? node) => string.IsNullOrWhiteSpace(AsString(node));

    private static string? AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string ListExample(KindQuestion q) =>
        q.Recommended?.Split(';')[0].Trim() is { Length: > 0 } first ? first : "first item";
}
