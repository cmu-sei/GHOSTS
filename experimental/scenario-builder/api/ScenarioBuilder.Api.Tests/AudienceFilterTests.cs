// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class AudienceFilterTests
{
    private static readonly Domain.Kind Tabletop = new KindCatalog(ApiFactory.KindsDirectory).Find(TestApi.Kind)!;

    /// <summary>A finished scenario whose ground truth and motive say something recognisable.</summary>
    private static async Task<JsonObject> FinishedDocument()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 3);
        await AnswerStage(owner, id, 3);
        await Put(owner, id, "groundTruth", new { value = new[] { "The generators failed their test" } });
        await Put(owner, id, "motive", new { value = "Fees" });
        return (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!;
    }

    [Fact]
    public async Task Participants_lose_the_ledger_ground_truth_and_excon_answers()
    {
        var doc = AudienceFilter.For(await FinishedDocument(), Tabletop, Audience.Participants);

        foreach (var gone in new[] { "decisions", "findings", "groundTruth", "teachingPoints" })
            Assert.Null(doc[gone]);
        var scenario = doc["scenario"]!.AsObject();
        Assert.Null(scenario["motive"]);
        Assert.Null(scenario["obviousExplanation"]);
        Assert.Null(scenario["executiveSummary"]);
        Assert.NotNull(scenario["problem"]);
        Assert.NotNull(doc["objectives"]);
        Assert.NotNull(doc["engagement"]!["participants"]);
        Assert.DoesNotContain("generators failed", doc.ToJsonString());
    }

    [Fact]
    public async Task Facilitators_keep_exercise_control_material_but_not_the_ledger()
    {
        var doc = AudienceFilter.For(await FinishedDocument(), Tabletop, Audience.Facilitators);

        Assert.Equal("The generators failed their test", (string)doc["groundTruth"]![0]!["text"]!);
        Assert.Equal("Fees", (string)doc["scenario"]!["motive"]!);
        Assert.Null(doc["decisions"]);
        Assert.Null(doc["findings"]);
    }

    [Fact]
    public async Task Designers_get_everything()
    {
        var original = await FinishedDocument();

        Assert.True(JsonNode.DeepEquals(original, AudienceFilter.For(original, Tabletop, Audience.Designers)));
    }

    [Fact]
    public void Emptied_containers_are_dropped()
    {
        var doc = new JsonObject { ["name"] = "x", ["scenario"] = new JsonObject { ["motive"] = "Fees" } };

        var filtered = AudienceFilter.For(doc, Tabletop, Audience.Participants);

        Assert.Null(filtered["scenario"]);
        Assert.Equal("x", (string)filtered["name"]!);
    }

    [Fact]
    public async Task The_input_is_left_alone()
    {
        var original = await FinishedDocument();

        AudienceFilter.For(original, Tabletop, Audience.Participants);

        Assert.NotNull(original["decisions"]);
        Assert.NotNull(original["groundTruth"]);
    }
}
