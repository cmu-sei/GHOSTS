// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using ScenarioBuilder.Api.Domain;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class PullTests
{
    private static readonly string SchemaPath =
        Path.GetFullPath(Path.Combine(ApiFactory.KindsDirectory, "../schema/exercise-document.v0.schema.json"));

    /// <summary>Built once: JsonSchema.Net registers a schema's $id globally and refuses a second registration.</summary>
    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(SchemaPath));

    private static async Task<JsonObject> Pull(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!;

    [Fact]
    public async Task Pull_assembles_answers_at_their_paths()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 1);
        await Put(owner, id, "objectives", new { value = new[] { "Assess", "Decide" } });
        await Put(owner, id, "teachingPoints", new { value = new[] { "Backups" } });

        var doc = await Pull(owner, id);

        Assert.Equal("0.3.0", (string)doc["schemaVersion"]!);
        Assert.Equal("crucible-tabletop", (string)doc["kind"]!["id"]!);
        Assert.Equal("harbor-blackout", (string)doc["slug"]!);
        Assert.Equal("draft", (string)doc["status"]!);
        Assert.Equal(1, (int)doc["stage"]!);
        Assert.Equal("Yes", (string)doc["engagement"]!["plainEnglish"]!);
        Assert.Equal("Answer for participants", (string)doc["engagement"]!["participants"]!);
        Assert.Equal("O2", (string)doc["objectives"]![1]!["id"]!);
        Assert.Equal("Decide", (string)doc["objectives"]![1]!["text"]!);
        Assert.Null(doc["objectives"]![1]!["visibility"]);
        Assert.Equal("Backups", (string)doc["teachingPoints"]![0]!["text"]!);
        Assert.Equal("excon", (string)doc["teachingPoints"]![0]!["visibility"]!);
        // Stage 0's, the two above, and the exercise name, which starts as the scenario's name; in the kind's order.
        var stage0 = KindOf(TestApi.Kind).Stages[0].Questions;
        Assert.Equal(stage0.Count + 3, doc["decisions"]!.AsArray().Count);
        Assert.Equal(stage0[0].Id, (string)doc["decisions"]![0]!["question"]!);
    }

    [Fact]
    public async Task Pull_places_drafts_and_plain_lists()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, KindOf(TestApi.Kind).Find("organizations")!.Value.Stage);
        await Put(owner, id, "organizations", new { value = new[] { "Coastline Data Centre", "Harbor Booking Service" } });

        var doc = await Pull(owner, id);

        Assert.Equal("A summary.", (string)doc["scenario"]!["executiveSummary"]!);
        Assert.Equal("Harbor Booking Service", (string)doc["names"]!["organizations"]![1]!);
        Assert.Equal("GT1", (string)doc["groundTruth"]![0]!["id"]!);
        Assert.Equal("excon", (string)doc["groundTruth"]![0]!["visibility"]!);
    }

    [Fact]
    public async Task Pull_carries_open_findings()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var findings = (await Pull(owner, id))["findings"]!.AsArray();

        Assert.Equal(KindOf(TestApi.Kind).Stages[0].Questions.Count, findings.Count);
        Assert.All(findings, f =>
        {
            Assert.Equal("stop", (string)f!["severity"]!);
            Assert.Equal("QUESTION_OPEN", (string)f["code"]!);
        });
    }

    [Fact]
    public async Task Network_considerations_land_under_network_and_planted_items_stay_with_control()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var smallUnit = KindOf(SmallUnitKind);
        var id = await CreateScenario(owner, "Team Drill", SmallUnitKind);
        await ReachStage(owner, id, 3, smallUnit);
        await Put(owner, id, "segments", new { value = new[] { "OT network: plant control", "Corporate LAN: 40 users" } });
        await Put(owner, id, "planted", new { value = new[] { "Infected contractor laptop on the corporate LAN" } });
        await Put(owner, id, "connectivity", new { value = "The historian bridges the corporate LAN and the OT network." });

        var doc = await Pull(owner, id);

        Assert.Equal(["OT network: plant control", "Corporate LAN: 40 users"], doc["network"]!["segments"]!.AsArray().Select(s => (string)s!));
        Assert.Equal("The historian bridges the corporate LAN and the OT network.", (string)doc["network"]!["connectivity"]!);
        Assert.Equal("excon", (string)doc["network"]!["planted"]![0]!["visibility"]!);
        var team = AudienceFilter.For(doc, smallUnit, Audience.Participants);
        Assert.NotNull(team["network"]!["segments"]);
        Assert.Null(team["network"]!["planted"]);
        var markdown = (await owner.GetStringAsync($"/api/scenarios/{id}/export?format=markdown")).Replace("\r\n", "\n");
        Assert.Contains("\n## Stage 3: Network and Range\n", markdown);
        Assert.Contains("- **Network Segments:**\n  - OT network: plant control\n", markdown);
    }

    [Fact]
    public async Task Pull_validates_against_the_schema()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var schema = Schema;
        var fresh = await CreateScenario(owner, "Fresh");
        var finished = await CreateScenario(owner, "Finished");
        await ReachStage(owner, finished, 3);
        await AnswerStage(owner, finished, 3);
        var smallUnit = KindOf(SmallUnitKind);
        var team = await CreateScenario(owner, "Team Drill", SmallUnitKind);
        await ReachStage(owner, team, smallUnit.LastStage, smallUnit);
        await AnswerStage(owner, team, smallUnit.LastStage, smallUnit);

        foreach (var id in new[] { fresh, finished, team })
        {
            using var doc = JsonDocument.Parse(await owner.GetStringAsync($"/api/scenarios/{id}"));
            var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.True(result.IsValid, string.Join("; ", (result.Details ?? []).Where(d => d.Errors is not null)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
        }
    }

    [Fact]
    public async Task Schema_rejects_an_unknown_top_level_field()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var schema = Schema;
        var doc = await Pull(owner, await CreateScenario(owner));
        doc["surprise"] = true;

        using var parsed = JsonDocument.Parse(doc.ToJsonString());
        Assert.False(schema.Evaluate(parsed.RootElement).IsValid);
    }

    [Fact]
    public async Task Unicode_and_long_text_round_trip()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        var text = "Données — 記録 " + new string('x', 5000);

        await Put(owner, id, "recording", new { value = text });

        Assert.Equal(text, (string)(await Pull(owner, id))["engagement"]!["recording"]!);
    }

    [Fact]
    public async Task Unknown_scenario_is_404()
    {
        using var factory = new ApiFactory();

        var response = await factory.Client().GetAsync("/api/scenarios/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("SCENARIO_UNKNOWN", await Code(response));
    }
}
