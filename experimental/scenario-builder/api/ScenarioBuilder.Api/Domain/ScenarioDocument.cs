// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

/// <summary>
/// The full scenario JSON that consumers pull, assembled from the ledger on every read. Each answer sits at its
/// question's path, each draft at its own; nothing in it is stored twice.
/// </summary>
public static class ScenarioDocument
{
    public const string SchemaVersion = "0.5.0";

    public static JsonObject Build(ScenarioType scenarioType, Scenario scenario, IReadOnlyList<Decision> decisions, Enclave? enclave = null)
    {
        var doc = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["scenarioType"] = new JsonObject { ["id"] = scenarioType.Id, ["version"] = scenario.ScenarioTypeVersion },
            ["slug"] = scenario.Slug,
            ["name"] = scenario.Name,
            ["status"] = "draft",
            ["stage"] = scenario.Stage
        };

        var byQuestion = decisions.ToDictionary(d => d.QuestionId);
        var record = new JsonArray();
        foreach (var q in scenarioType.AllQuestions)
        {
            if (!byQuestion.TryGetValue(q.Id, out var decision))
                continue;
            var value = JsonNode.Parse(decision.Value);
            Place(doc, q.Path, Shape(q, value));
            var entry = new JsonObject
            {
                ["question"] = q.Id,
                ["value"] = value?.DeepClone(),
                ["who"] = Name(decision.Who),
                ["by"] = decision.DecidedBy,
                ["via"] = Name(decision.Via),
                ["time"] = decision.Time
            };
            if (decision.Reason is not null)
                entry["reason"] = decision.Reason;
            record.Add(entry);
        }

        var drafts = Ledger.ReadDrafts(scenario);
        foreach (var draft in scenarioType.Drafts)
            if (drafts[draft.Field] is JsonValue text && !string.IsNullOrWhiteSpace(text.GetValue<string>()))
                Place(doc, draft.Path, text.DeepClone());

        doc["decisions"] = record;
        doc["findings"] = new JsonArray(Ledger.Progress(scenarioType, scenario, decisions).Gate.Findings
            .Select(f => (JsonNode?)new JsonObject { ["severity"] = f.Severity, ["code"] = f.Code, ["message"] = f.Message, ["stage"] = f.Stage })
            .ToArray());
        if (enclave is not null)
            doc["deployments"] = new JsonArray(Deployment(enclave));
        return doc;
    }

    /// <summary>The deployment built for the scenario on GraySpace: its enclave's slug, and the version GraySpace stored once it's saved there.</summary>
    private static JsonObject Deployment(Enclave enclave)
    {
        var deployment = new JsonObject { ["target"] = "grayspace" };
        if (JsonNode.Parse(enclave.Spec)?["slug"] is JsonValue slug)
            deployment["enclave"] = slug.DeepClone();
        if (enclave.Version is not null)
            deployment["version"] = enclave.Version;
        return deployment;
    }

    /// <summary>A list answer with an id prefix or a visibility becomes items; every other answer stays as it is.</summary>
    private static JsonNode? Shape(ScenarioTypeQuestion q, JsonNode? value)
    {
        if (q.Answer != AnswerType.List || value is not JsonArray list || (q.IdPrefix is null && q.Visibility is null))
            return value;
        var items = new JsonArray();
        for (var i = 0; i < list.Count; i++)
        {
            var item = new JsonObject();
            if (q.IdPrefix is not null)
                item["id"] = $"{q.IdPrefix}{i + 1}";
            item["text"] = list[i]?.DeepClone();
            if (q.Visibility is not null)
                item["visibility"] = q.Visibility;
            items.Add(item);
        }
        return items;
    }

    /// <summary>Sets <paramref name="value"/> at a path like /engagement/participants, creating objects on the way.</summary>
    private static void Place(JsonObject root, string path, JsonNode? value)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = root;
        foreach (var part in parts[..^1])
        {
            if (node[part] is not JsonObject child)
                node[part] = child = [];
            node = child;
        }
        node[parts[^1]] = value;
    }

    private static string Name<T>(T value) where T : Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
