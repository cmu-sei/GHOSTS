// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ScenarioBuilder.Api.Infrastructure;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class ScenarioTypeUpdateTests
{
    private const string Id = "red-team-dream";

    private static object Question(string id, string text, string answer = "text", string[]? options = null) =>
        new { id, text, why = "w", answer, options, path = $"/scenario/{id}" };

    /// <summary>Version 0.1.0: two stages, and an executive summary after the first.</summary>
    private static readonly object First = new
    {
        id = Id, version = "0.0.0", name = "Red Team Dream", topology = false,
        drafts = new[] { new { field = "executiveSummary", dueAfterStage = 0, path = "/scenario/executiveSummary" } },
        stages = new object[]
        {
            new { number = 0, title = "Target", questions = new[]
            {
                Question("target", "What does the team attack?"),
                Question("rules", "How strict are the rules?", "choice", ["Strict", "Loose"]),
                Question("notes", "Anything else?")
            } },
            new { number = 1, title = "Story", questions = new[] { Question("story", "What happened?") } }
        }
    };

    /// <summary>
    /// Version 0.2.0: one stage. target is reworded, rules lost the option "Loose", notes is gone, story moved into
    /// stage 0, scope is new, and there's no executive summary any more.
    /// </summary>
    private static readonly object Second = new
    {
        id = Id, version = "0.0.0", name = "Red Team Dream", topology = true, drafts = Array.Empty<object>(),
        stages = new object[]
        {
            new { number = 0, title = "Target", questions = new[]
            {
                Question("target", "Which network does the team attack?"),
                Question("rules", "How strict are the rules?", "choice", ["Strict", "Medium"]),
                Question("story", "What happened?"),
                Question("scope", "What is out of scope?")
            } }
        }
    };

    private static async Task Publish(HttpClient admin, object scenarioType)
    {
        (await admin.PutAsJsonAsync($"/api/admin/scenario-types/{Id}", scenarioType)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/scenario-types/{Id}/publish", null)).EnsureSuccessStatusCode();
    }

    /// <summary>A scenario on 0.1.0 with every question answered and the summary drafted, then 0.2.0 published.</summary>
    private static async Task<int> OnTheFirstVersion(ApiFactory factory, HttpClient owner)
    {
        (await owner.PostAsJsonAsync("/api/admin/scenario-types", new { name = "Red Team Dream" })).EnsureSuccessStatusCode();
        await Publish(owner, First);
        var id = await CreateScenario(owner, "Sincere Buzzsaw", Id);
        foreach (var (question, value) in new[] { ("target", "The bank"), ("rules", "Loose"), ("notes", "Bring coffee.") })
            (await Put(owner, id, question, new { value })).EnsureSuccessStatusCode();
        (await Draft(owner, id, "A summary.")).EnsureSuccessStatusCode();
        (await Advance(owner, id)).EnsureSuccessStatusCode();
        (await Put(owner, id, "story", new { value = "They got in." })).EnsureSuccessStatusCode();
        await Publish(owner, Second);
        return id;
    }

    private static Task<HttpResponseMessage> Update(HttpClient client, int id, string version = "0.2.0") =>
        client.PostAsJsonAsync($"/api/scenarios/{id}/scenario-type-update", new { version });

    [Fact]
    public async Task The_preview_says_what_carries_over_what_is_dropped_and_what_is_new()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await OnTheFirstVersion(factory, owner);

        var plan = (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}/scenario-type-update"))!;

        Assert.Equal(("0.1.0", "0.2.0"), ((string)plan["from"]!, (string)plan["to"]!));
        Assert.Equal(["target:0>0", "story:1>0"], plan["carried"]!.AsArray().Select(c => $"{c!["id"]}:{c["fromStage"]}>{c["toStage"]}"));
        var dropped = plan["dropped"]!.AsArray();
        Assert.Equal(["rules", "notes"], dropped.Select(d => (string)d!["id"]!));
        Assert.Contains("Strict, Medium", (string)dropped[0]!["why"]!);
        Assert.Contains("0.2.0", (string)dropped[1]!["why"]!);
        Assert.Equal(["scope:0"], plan["added"]!.AsArray().Select(a => $"{a!["id"]}:{a["stage"]}"));
        Assert.Equal(["executiveSummary"], plan["draftsDropped"]!.AsArray().Select(d => (string)d!));
        Assert.Equal((1, 0), ((int)plan["stageFrom"]!, (int)plan["stageTo"]!));
        Assert.Equal("dev", (string)plan["owner"]!);
    }

    [Fact]
    public async Task Updating_carries_answers_over_and_leaves_the_new_and_dropped_ones_to_answer()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await OnTheFirstVersion(factory, owner);
        (await factory.Client(agent: true).PostAsJsonAsync($"/api/scenarios/{id}/proposals", new { questionId = "notes", value = "Bring tea.", reason = "Tea." }))
            .EnsureSuccessStatusCode();

        var updated = await Update(owner, id);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var progress = await Progress(owner, id);
        Assert.Equal("0.2.0", (string)progress["scenarioTypeVersion"]!);
        Assert.Equal(0, (int)progress["stage"]!);
        Assert.Equal(["rules", "scope"], progress["open"]!.AsArray().Select(q => (string)q!["id"]!));
        Assert.Equal(["target", "story"], progress["answered"]!.AsArray().Select(a => (string)a!["id"]!));
        Assert.Equal(0, (int)progress["answered"]![1]!["stage"]!);
        Assert.Equal("0.2.0", (string)(await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!["scenarioType"]!["version"]!);
        Assert.Empty((await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/proposals?status=pending"))!);

        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;
        Assert.Contains(log, e => (string)e!["target"]! == "notes" && (string)e["before"]! == "Bring coffee." && e["after"] is null);
        Assert.Contains(log, e => (string)e!["type"]! == "update" && (string)e["before"]! == "0.1.0" && (string)e["after"]! == "0.2.0");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuilderDb>();
        Assert.Equal("{}", db.Scenarios.Single(s => s.Id == id).Drafts.Replace(" ", ""));
    }

    [Fact]
    public async Task Only_the_owner_updates_and_only_to_the_version_they_reviewed()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await OnTheFirstVersion(factory, owner);

        Assert.Equal("NOT_OWNER", await Code(await Update(factory.Client(user: "lee"), id)));
        Assert.Equal("AGENT_CANNOT_UPDATE", await Code(await Update(factory.Client(agent: true), id)));
        var stale = await Update(owner, id, "0.9.0");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_CHANGED", await Code(stale));

        (await Update(owner, id)).EnsureSuccessStatusCode();
        var again = await Update(owner, id);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("UP_TO_DATE", await Code(again));
        var plan = (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}/scenario-type-update"))!;
        Assert.Equal((string)plan["from"]!, (string)plan["to"]!);
        Assert.Empty(plan["dropped"]!.AsArray());
    }
}
