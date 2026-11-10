// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

/// <summary>The rules of the question ledger, as pure functions over a scenario type and a scenario's rows.</summary>
public static class Ledger
{
    /// <summary>How answers are stored: readable text, not \u escapes.</summary>
    public static readonly JsonSerializerOptions Storage = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Null when <paramref name="value"/> fits the question; otherwise what it takes, worded for the agent.</summary>
    public static string? ValidateValue(ScenarioTypeQuestion q, JsonNode? value, out JsonNode? canonical)
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
    /// The open stage and its gate. The gate passes when every question is answered and every draft due after the stage
    /// is written.
    /// </summary>
    public static ProgressView Progress(ScenarioType scenarioType, Scenario scenario, IReadOnlyList<Decision> decisions)
    {
        var stage = scenarioType.Stages[scenario.Stage];
        var answered = decisions.ToDictionary(d => d.QuestionId);
        // Every stage up to the open one: a question the scenario type gained after its stage closed is asked like any other.
        var open = scenarioType.Stages.Take(scenario.Stage + 1)
            .SelectMany(s => s.Questions.Where(q => !answered.ContainsKey(q.Id)).Select(q => QuestionView.From(q, s.Number))).ToList();
        var findings = open.Select(q => new Finding("stop", "QUESTION_OPEN", $"'{q.Id}' is not answered.", q.Stage)).ToList();

        var drafts = ReadDrafts(scenario);
        List<string> draftsDue = open.All(q => q.Stage != stage.Number)
            ? scenarioType.Drafts.Where(d => d.DueAfterStage == stage.Number && IsBlank(drafts[d.Field])).Select(d => d.Field).ToList()
            : [];
        findings.AddRange(draftsDue.Select(f => new Finding("stop", "DRAFT_MISSING", $"The {f} draft isn't written yet.", stage.Number)));

        return new ProgressView(
            scenarioType.Id,
            scenario.Stage,
            stage.Title,
            scenario.Stage == scenarioType.LastStage,
            new GateView(findings.Count == 0, findings),
            open,
            scenarioType.AllQuestions.Where(q => answered.ContainsKey(q.Id)).Select(q => AnsweredView.From(answered[q.Id])).ToList(),
            draftsDue,
            scenario.Delegated) { ScenarioTypeVersion = scenarioType.Version };
    }

    public static JsonObject ReadDrafts(Scenario scenario) => JsonNode.Parse(scenario.Drafts) as JsonObject ?? [];

    public static bool StageAnswered(ScenarioType scenarioType, int stage, IReadOnlyList<Decision> decisions) =>
        scenarioType.Stages[stage].Questions.All(q => decisions.Any(d => d.QuestionId == q.Id));

    private static bool IsBlank(JsonNode? node) => string.IsNullOrWhiteSpace(AsString(node));

    private static string? AsString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string ListExample(ScenarioTypeQuestion q) =>
        q.Recommended?.Split(';')[0].Trim() is { Length: > 0 } first ? first : "first item";
}
