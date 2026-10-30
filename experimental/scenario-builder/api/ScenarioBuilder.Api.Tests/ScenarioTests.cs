// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class ScenarioTests
{
    [Fact]
    public async Task A_new_scenario_takes_its_name_as_the_exercise_name()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client(user: "ana");
        var id = await CreateScenario(owner, "Harbor Blackout");

        var answer = (await Progress(owner, id))["answered"]!.AsArray().Single(a => (string)a!["id"]! == "exerciseName")!;
        Assert.Equal("Harbor Blackout", (string)answer["value"]!);
        Assert.Equal("stated", (string)answer["who"]!);
        Assert.Equal("ana", (string)answer["by"]!);
        Assert.Equal("Defaults to the scenario's name.", (string)answer["reason"]!);
        var document = (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!;
        Assert.Equal("Harbor Blackout", (string)document["names"]!["exercise"]!);
        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;
        Assert.Contains(log, e => (string)e!["target"]! == "exerciseName" && (string)e["after"]! == "Harbor Blackout");

        SetStage(factory, id, KindOf(Kind).Find("exerciseName")!.Value.Stage);
        (await Put(owner, id, "exerciseName", new { value = "Still Water" })).EnsureSuccessStatusCode();
        Assert.Equal("Still Water", (string)(await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!["names"]!["exercise"]!);
    }

    [Fact]
    public async Task Create_then_list()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();

        var response = await client.PostAsJsonAsync("/api/scenarios", new { name = "Harbor Blackout", kindId = Kind });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Body(response);
        Assert.Equal("harbor-blackout", (string)created["slug"]!);
        Assert.Equal("dev", (string)created["owner"]!);
        Assert.Equal(0, (int)created["stage"]!);
        Assert.Equal("0.3.0", (string)created["kindVersion"]!);

        var list = await client.GetFromJsonAsync<JsonArray>("/api/scenarios");
        Assert.Equal("Harbor Blackout", (string)list![0]!["name"]!);
    }

    [Fact]
    public async Task Same_name_gets_numbered_slug()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();

        await CreateScenario(client);
        var second = await client.PostAsJsonAsync("/api/scenarios", new { name = "Harbor  Blackout!", kindId = Kind });

        Assert.Equal("harbor-blackout-2", (string)(await Body(second))["slug"]!);
    }

    [Fact]
    public async Task Unknown_kind_is_400()
    {
        using var factory = new ApiFactory();

        var response = await factory.Client().PostAsJsonAsync("/api/scenarios", new { name = "x", kindId = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("KIND_UNKNOWN", await Code(response));
    }

    [Fact]
    public async Task Blank_name_is_400()
    {
        using var factory = new ApiFactory();

        var response = await factory.Client().PostAsJsonAsync("/api/scenarios", new { name = "  ", kindId = Kind });

        Assert.Equal("NAME_REQUIRED", await Code(response));
    }

    [Fact]
    public async Task New_scenario_progress_opens_stage_0()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();
        var id = await CreateScenario(client);

        var progress = await Progress(client, id);

        Assert.Equal(Kind, (string)progress["kindId"]!);
        Assert.Equal(0, (int)progress["stage"]!);
        Assert.Equal("Engagement Context", (string)progress["stageTitle"]!);
        var open = progress["open"]!.AsArray();
        Assert.Equal(KindOf(Kind).Stages[0].Questions.Count, open.Count);
        Assert.Equal("intent", (string)open[0]!["id"]!);
        Assert.False((bool)progress["gate"]!["passes"]!);
        Assert.All(progress["gate"]!["findings"]!.AsArray(), f => Assert.Equal("QUESTION_OPEN", (string)f!["code"]!));
    }

    [Fact]
    public async Task Unknown_scenario_progress_is_404()
    {
        using var factory = new ApiFactory();

        var response = await factory.Client().GetAsync("/api/scenarios/999/progress");

        Assert.Equal("SCENARIO_UNKNOWN", await Code(response));
    }
}
