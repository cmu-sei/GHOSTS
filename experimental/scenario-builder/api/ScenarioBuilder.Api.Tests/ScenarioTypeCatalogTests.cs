// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Tests;

public class ScenarioTypeCatalogTests
{
    [Fact]
    public void Tabletop_kind_loads_in_five_stages()
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find("crucible-tabletop")!;

        Assert.Equal("0.3.0", scenarioType.Version);
        Assert.Equal([0, 1, 2, 3, 4], scenarioType.Stages.Select(s => s.Number));
        Assert.Equal("Events, Decisions and Rules of Play", scenarioType.Stages[3].Title);
        var groundTruth = scenarioType.Find("groundTruth")!.Value;
        Assert.Equal(("groundTruth", 2), (groundTruth.Question.Id, groundTruth.Stage));
        Assert.Equal(AnswerType.Choice, scenarioType.Find("actorType")!.Value.Question.Answer);
    }

    [Fact]
    public void Every_tabletop_question_has_a_short_label()
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find("crucible-tabletop")!;

        Assert.Equal("Plain English", scenarioType.Find("plainEnglish")!.Value.Question.Label);
        Assert.All(scenarioType.AllQuestions, q => Assert.False(string.IsNullOrWhiteSpace(q.Label), q.Id));
    }

    [Fact]
    public void Tabletop_kind_marks_what_gives_the_game_away()
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find("crucible-tabletop")!;
        string? Visibility(string id) => scenarioType.Find(id)!.Value.Question.Visibility;

        foreach (var id in new[] { "obviousExplanation", "groundTruth", "motive", "attackPath", "ending", "redHerring", "escalationCeiling", "teachingPoints" })
            Assert.Equal("excon", Visibility(id));
        Assert.Equal("excon", scenarioType.Drafts.Single(d => d.Field == "executiveSummary").Visibility);
        Assert.Null(Visibility("participants"));
        Assert.Null(Visibility("problem"));
        Assert.Null(Visibility("objectives"));
    }

    [Fact]
    public void Unknown_visibility_fails_at_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-scenario-type-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "v.scenario-type.json"), """
            { "id": "v", "version": "1", "name": "V", "drafts": [],
              "stages": [ { "number": 0, "title": "S", "questions": [
                { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a", "visibility": "secret" } ] } ] }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => new ScenarioTypeCatalog(dir));
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public void A_stage_gate_from_an_older_version_still_loads_and_is_not_written_back()
    {
        // Older published versions, kept in the database, have a "gate" on every stage.
        var scenarioType = ScenarioTypeCatalog.Read("""
            { "id": "g", "version": "1", "name": "G", "drafts": [],
              "stages": [ { "number": 0, "title": "S", "gate": { "approval": "Approve it" }, "questions": [
                { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a" } ] } ] }
            """, "g.scenario-type.json");

        Assert.Equal("S", scenarioType.Stages[0].Title);
        Assert.DoesNotContain("gate", ScenarioTypeStore.Write(scenarioType));
    }

    [Fact]
    public void Both_scenario_types_load_under_their_names()
    {
        Assert.Equal(["Tabletop Exercise (TTX)", "Small Unit Team Exercise"], new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).All.Select(k => k.Name));
    }

    [Fact]
    public void Small_unit_kind_trains_tasks_to_standards_and_keeps_what_controllers_know_from_the_team()
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find(TestApi.SmallUnitType)!;

        Assert.Equal(["Team and Setting", "Tasks, Conditions and Standards", "Scenario, Opposition and Ground Truth", "Network and Range", "Control, Safety and Names"],
            scenarioType.Stages.Select(s => s.Title));
        Assert.Equal(("/objectives", "T"), (scenarioType.Find("tasks")!.Value.Question.Path, scenarioType.Find("tasks")!.Value.Question.IdPrefix));
        foreach (var id in new[] { "groundTruth", "keyEvents", "escalationCeiling", "ending", "teachingPoints" })
            Assert.Equal("excon", scenarioType.Find(id)!.Value.Question.Visibility);
        foreach (var id in new[] { "unit", "standards", "systems", "safety" })
            Assert.Null(scenarioType.Find(id)!.Value.Question.Visibility);
        Assert.Equal("scenarioName", scenarioType.Find("exerciseName")!.Value.Question.Default);
        Assert.Equal("excon", scenarioType.Drafts.Single().Visibility);
    }

    [Fact]
    public void Both_scenario_types_capture_network_considerations_for_the_network_module()
    {
        var catalog = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory);
        var smallUnit = catalog.Find(TestApi.SmallUnitType)!;
        var tabletop = catalog.Find(TestApi.TabletopType)!;

        var network = smallUnit.Stages[3].Questions;
        Assert.Equal(["rangeReference", "segments", "hosts", "services", "connectivity", "identity", "monitoring", "telemetry", "platforms", "simulatedUsers", "planted", "buildLimits"],
            network.Select(q => q.Id));
        Assert.All(network, q => Assert.StartsWith("/network/", q.Path));
        Assert.Equal(["simulatedUsers", "planted"], network.Where(q => q.Visibility == "excon").Select(q => q.Id));

        Assert.Equal((2, "/network/notional"), (tabletop.Find("notionalNetwork")!.Value.Stage, tabletop.Find("notionalNetwork")!.Value.Question.Path));
        Assert.Equal("/network/liveRange", tabletop.Find("liveRange")!.Value.Question.Path);
        Assert.All(new[] { smallUnit, tabletop }, k => Assert.Equal("0.3.0", k.Version));
    }

    [Fact]
    public void Only_the_small_unit_kind_has_a_topology_page()
    {
        var catalog = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory);

        Assert.True(catalog.Find(TestApi.SmallUnitType)!.Topology);
        Assert.False(catalog.Find(TestApi.TabletopType)!.Topology);
    }

    // What the GHOSTS scenario builder captures from a person (its A1–E3 checklist and planner form), by the question here that captures it.
    private static readonly string[] GhostsChecklist =
    [
        "intent", "proficiency", "mandate", "rulesOfEngagement", "trainingKind", "adjudication", "pacing", "description", "placeAndTime",
        "actorCapability", "attackTechniques", "keyEvents", "branching", "escalationSteps", "fog", "deadline"
    ];

    [Theory]
    [InlineData(TestApi.TabletopType, new[] { "successCriteria", "victoryConditions", "scenarioClock", "decisionPoints", "population", "informationEnvironment" })]
    [InlineData(TestApi.SmallUnitType, new[] { "standards", "adversary", "rangeReference", "services", "telemetry", "platforms" })]
    public void Both_scenario_types_capture_what_the_ghosts_builder_captures(string scenarioTypeId, string[] own)
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find(scenarioTypeId)!;

        var missing = GhostsChecklist.Concat(own).Where(id => scenarioType.Find(id) is null).ToList();
        Assert.Empty(missing);
        foreach (var id in new[] { "intent", "actorCapability", "attackTechniques", "keyEvents", "branching", "escalationSteps" })
            Assert.Equal("excon", scenarioType.Find(id)!.Value.Question.Visibility);
        foreach (var id in new[] { "description", "rulesOfEngagement", "proficiency", "mandate" })
            Assert.Null(scenarioType.Find(id)!.Value.Question.Visibility);
        Assert.Contains("friendly", scenarioType.Find("organizations")!.Value.Question.Text);
    }

    [Fact]
    public void Unknown_default_fails_at_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-scenario-type-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "d.scenario-type.json"), """
            { "id": "d", "version": "1", "name": "D", "drafts": [],
              "stages": [ { "number": 0, "title": "S", "questions": [
                { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a", "default": "ownerName" } ] } ] }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => new ScenarioTypeCatalog(dir));
        Assert.Contains("'a'", ex.Message);
        Assert.Contains("scenarioName", ex.Message);
    }

    [Fact]
    public void Tabletop_exercise_name_defaults_to_the_scenario_name()
    {
        var scenarioType = new ScenarioTypeCatalog(ApiFactory.ScenarioTypesDirectory).Find(TestApi.TabletopType)!;

        Assert.Equal("scenarioName", scenarioType.Find("exerciseName")!.Value.Question.Default);
    }

    [Fact]
    public void Choice_recommendation_outside_its_options_fails_at_load()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ScenarioTypeCatalog(TempTypeDir(duplicateIds: false, badChoice: true)));
        Assert.Contains("recommended", ex.Message);
    }

    [Fact]
    public void Duplicate_question_ids_fail_at_load()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ScenarioTypeCatalog(TempTypeDir(duplicateIds: true, badChoice: false)));
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public async Task Scenario_types_endpoint_lists_and_returns_the_scenario_type()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();

        var list = await client.GetFromJsonAsync<JsonArray>("/api/scenario-types");
        Assert.Equal("crucible-tabletop", (string)list![0]!["id"]!);

        var scenarioType = await client.GetFromJsonAsync<JsonObject>("/api/scenario-types/crucible-tabletop");
        var actorType = scenarioType!["stages"]![2]!["questions"]!.AsArray().Single(q => (string)q!["id"]! == "actorType")!;
        Assert.Equal("choice", (string)actorType["answer"]!);

        var missing = await client.GetAsync("/api/scenario-types/nope");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("SCENARIO_TYPE_UNKNOWN", (string)(await missing.Content.ReadFromJsonAsync<JsonObject>())!["code"]!);
    }

    /// <summary>A directory holding one small scenario type, broken in the way asked for.</summary>
    private static string TempTypeDir(bool duplicateIds, bool badChoice)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-scenario-type-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var second = duplicateIds ? "a" : "b";
        var recommended = badChoice ? "z" : "x";
        File.WriteAllText(Path.Combine(dir, "small.scenario-type.json"), $$"""
            {
              "id": "small", "version": "0.0.1", "name": "Small",
              "stages": [
                { "number": 0, "title": "Only", "questions": [
                  { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a" },
                  { "id": "{{second}}", "text": "B?", "why": "w", "answer": "choice", "options": ["x", "y"],
                    "recommended": "{{recommended}}", "path": "/b" }
                ] }
              ],
              "drafts": []
            }
            """);
        return dir;
    }
}
