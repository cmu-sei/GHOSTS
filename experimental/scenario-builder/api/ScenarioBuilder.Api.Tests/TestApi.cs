// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Tests;

/// <summary>Calls the tests share. Writes go through the HTTP API; only setup shortcuts touch the database.</summary>
public static class TestApi
{
    public const string Kind = "crucible-tabletop";
    public const string SmallUnitKind = "small-unit-team-exercise";

    public static async Task<int> CreateScenario(HttpClient client, string name = "Harbor Blackout", string kindId = Kind)
    {
        var response = await client.PostAsJsonAsync("/api/scenarios", new { name, kindId });
        response.EnsureSuccessStatusCode();
        return (int)(await Body(response))["id"]!;
    }

    public static Task<HttpResponseMessage> Put(HttpClient client, int scenarioId, string questionId, object body) =>
        client.PutAsJsonAsync($"/api/scenarios/{scenarioId}/decisions/{questionId}", body);

    public static async Task<JsonObject> Progress(HttpClient client, int scenarioId) =>
        (await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{scenarioId}/progress"))!;

    public static async Task<JsonObject> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonObject>())!;

    public static async Task<string> Code(HttpResponseMessage response) => (string)(await Body(response))["code"]!;

    private static readonly KindCatalog Kinds = new(ApiFactory.KindsDirectory);
    private static readonly Kind Tabletop = Kinds.Find(Kind)!;

    public static Kind KindOf(string id) => Kinds.Find(id)!;

    /// <summary>A value every question of its type accepts.</summary>
    public static object ValidValue(KindQuestion q) => q.Answer switch
    {
        AnswerType.Choice => q.Options![0],
        AnswerType.List => new[] { $"Item for {q.Id}" },
        _ => $"Answer for {q.Id}"
    };

    /// <summary>Answers every question of <paramref name="stage"/> as a person; the tabletop kind unless another is given.</summary>
    public static async Task AnswerStage(HttpClient client, int scenarioId, int stage, Kind? kind = null)
    {
        foreach (var q in (kind ?? Tabletop).Stages[stage].Questions)
            (await Put(client, scenarioId, q.Id, new { value = ValidValue(q) })).EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> Approve(HttpClient client, int scenarioId, int stage) =>
        client.PostAsync($"/api/scenarios/{scenarioId}/gates/{stage}/approve", null);

    public static Task<HttpResponseMessage> Advance(HttpClient client, int scenarioId) =>
        client.PostAsync($"/api/scenarios/{scenarioId}/advance", null);

    public static Task<HttpResponseMessage> Draft(HttpClient client, int scenarioId, string text, string field = "executiveSummary") =>
        client.PutAsJsonAsync($"/api/scenarios/{scenarioId}/drafts/{field}", new { text });

    /// <summary>Answers, drafts, approves and advances as the owner until <paramref name="target"/> is the open stage.</summary>
    public static async Task ReachStage(HttpClient owner, int scenarioId, int target, Kind? kind = null)
    {
        kind ??= Tabletop;
        for (var stage = 0; stage < target; stage++)
        {
            await AnswerStage(owner, scenarioId, stage, kind);
            if (kind.Drafts.Any(d => d.DueAfterStage == stage))
                (await Draft(owner, scenarioId, "A summary.")).EnsureSuccessStatusCode();
            if (kind.Stages[stage].Gate.Approval is not null)
                (await Approve(owner, scenarioId, stage)).EnsureSuccessStatusCode();
            (await Advance(owner, scenarioId)).EnsureSuccessStatusCode();
        }
    }

    public static async Task<string[]> FindingCodes(HttpClient client, int scenarioId) =>
        (await Progress(client, scenarioId))["gate"]!["findings"]!.AsArray().Select(f => (string)f!["code"]!).ToArray();

    /// <summary>Setup shortcut: open a later stage without answering the earlier ones.</summary>
    public static void SetStage(ApiFactory factory, int scenarioId, int stage)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuilderDb>();
        db.Scenarios.Single(s => s.Id == scenarioId).Stage = stage;
        db.SaveChanges();
    }
}
