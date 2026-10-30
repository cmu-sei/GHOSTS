// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Tests;

public class KindCatalogTests
{
    [Fact]
    public void Tabletop_kind_loads_in_five_stages()
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find("crucible-tabletop")!;

        Assert.Equal("0.3.0", kind.Version);
        Assert.Equal([0, 1, 2, 3, 4], kind.Stages.Select(s => s.Number));
        Assert.Equal("Events, Decisions and Rules of Play", kind.Stages[3].Title);
        Assert.Equal("Approve the names", kind.Stages[4].Gate.Approval);
        var groundTruth = kind.Find("groundTruth")!.Value;
        Assert.Equal(("groundTruth", 2), (groundTruth.Question.Id, groundTruth.Stage));
        Assert.Equal("Approve the executive summary", kind.Stages[2].Gate.Approval);
        Assert.Null(kind.Stages[1].Gate.Approval);
        Assert.Equal(AnswerType.Choice, kind.Find("actorType")!.Value.Question.Answer);
    }

    [Fact]
    public void Every_tabletop_question_has_a_short_label()
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find("crucible-tabletop")!;

        Assert.Equal("Plain English", kind.Find("plainEnglish")!.Value.Question.Label);
        Assert.All(kind.AllQuestions, q => Assert.False(string.IsNullOrWhiteSpace(q.Label), q.Id));
    }

    [Fact]
    public void Tabletop_kind_marks_what_gives_the_game_away()
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find("crucible-tabletop")!;
        string? Visibility(string id) => kind.Find(id)!.Value.Question.Visibility;

        foreach (var id in new[] { "obviousExplanation", "groundTruth", "motive", "attackPath", "ending", "redHerring", "escalationCeiling", "teachingPoints" })
            Assert.Equal("excon", Visibility(id));
        Assert.Equal("excon", kind.Drafts.Single(d => d.Field == "executiveSummary").Visibility);
        Assert.Null(Visibility("participants"));
        Assert.Null(Visibility("problem"));
        Assert.Null(Visibility("objectives"));
    }

    [Fact]
    public void Unknown_visibility_fails_at_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-kind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "v.kind.json"), """
            { "id": "v", "version": "1", "name": "V", "drafts": [],
              "stages": [ { "number": 0, "title": "S", "gate": {}, "questions": [
                { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a", "visibility": "secret" } ] } ] }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => new KindCatalog(dir));
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public void Both_scenario_types_load_under_their_names()
    {
        Assert.Equal(["Tabletop Exercise (TTX)", "Small Unit Team Exercise"], new KindCatalog(ApiFactory.KindsDirectory).All.Select(k => k.Name));
    }

    [Fact]
    public void Small_unit_kind_trains_tasks_to_standards_and_keeps_what_controllers_know_from_the_team()
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find(TestApi.SmallUnitKind)!;

        Assert.Equal(["Team and Setting", "Tasks, Conditions and Standards", "Scenario, Opposition and Ground Truth", "Network and Range", "Control, Safety and Names"],
            kind.Stages.Select(s => s.Title));
        Assert.Equal(("/objectives", "T"), (kind.Find("tasks")!.Value.Question.Path, kind.Find("tasks")!.Value.Question.IdPrefix));
        foreach (var id in new[] { "groundTruth", "keyEvents", "escalationCeiling", "ending", "teachingPoints" })
            Assert.Equal("excon", kind.Find(id)!.Value.Question.Visibility);
        foreach (var id in new[] { "unit", "standards", "systems", "safety" })
            Assert.Null(kind.Find(id)!.Value.Question.Visibility);
        Assert.Equal("scenarioName", kind.Find("exerciseName")!.Value.Question.Default);
        Assert.Equal("excon", kind.Drafts.Single().Visibility);
    }

    [Fact]
    public void Both_kinds_capture_network_considerations_for_the_network_module()
    {
        var catalog = new KindCatalog(ApiFactory.KindsDirectory);
        var smallUnit = catalog.Find(TestApi.SmallUnitKind)!;
        var tabletop = catalog.Find(TestApi.Kind)!;

        var network = smallUnit.Stages[3].Questions;
        Assert.Equal(["rangeReference", "segments", "hosts", "services", "connectivity", "identity", "monitoring", "telemetry", "platforms", "simulatedUsers", "planted", "buildLimits"],
            network.Select(q => q.Id));
        Assert.All(network, q => Assert.StartsWith("/network/", q.Path));
        Assert.Equal(["simulatedUsers", "planted"], network.Where(q => q.Visibility == "excon").Select(q => q.Id));
        Assert.Equal("Approve the network requirements", smallUnit.Stages[3].Gate.Approval);

        Assert.Equal((2, "/network/notional"), (tabletop.Find("notionalNetwork")!.Value.Stage, tabletop.Find("notionalNetwork")!.Value.Question.Path));
        Assert.Equal("/network/liveRange", tabletop.Find("liveRange")!.Value.Question.Path);
        Assert.All(new[] { smallUnit, tabletop }, k => Assert.Equal("0.3.0", k.Version));
    }

    // What the GHOSTS scenario builder captures from a person (its A1–E3 checklist and planner form), by the question here that captures it.
    private static readonly string[] GhostsChecklist =
    [
        "intent", "proficiency", "mandate", "rulesOfEngagement", "trainingKind", "adjudication", "pacing", "description", "placeAndTime",
        "actorCapability", "attackTechniques", "keyEvents", "branching", "escalationSteps", "fog", "deadline"
    ];

    [Theory]
    [InlineData(TestApi.Kind, new[] { "successCriteria", "victoryConditions", "scenarioClock", "decisionPoints", "population", "informationEnvironment" })]
    [InlineData(TestApi.SmallUnitKind, new[] { "standards", "adversary", "rangeReference", "services", "telemetry", "platforms" })]
    public void Both_kinds_capture_what_the_ghosts_builder_captures(string kindId, string[] own)
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find(kindId)!;

        var missing = GhostsChecklist.Concat(own).Where(id => kind.Find(id) is null).ToList();
        Assert.Empty(missing);
        foreach (var id in new[] { "intent", "actorCapability", "attackTechniques", "keyEvents", "branching", "escalationSteps" })
            Assert.Equal("excon", kind.Find(id)!.Value.Question.Visibility);
        foreach (var id in new[] { "description", "rulesOfEngagement", "proficiency", "mandate" })
            Assert.Null(kind.Find(id)!.Value.Question.Visibility);
        Assert.Contains("friendly", kind.Find("organizations")!.Value.Question.Text);
    }

    [Fact]
    public void Unknown_default_fails_at_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-kind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "d.kind.json"), """
            { "id": "d", "version": "1", "name": "D", "drafts": [],
              "stages": [ { "number": 0, "title": "S", "gate": {}, "questions": [
                { "id": "a", "text": "A?", "why": "w", "answer": "text", "path": "/a", "default": "ownerName" } ] } ] }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => new KindCatalog(dir));
        Assert.Contains("'a'", ex.Message);
        Assert.Contains("scenarioName", ex.Message);
    }

    [Fact]
    public void Tabletop_exercise_name_defaults_to_the_scenario_name()
    {
        var kind = new KindCatalog(ApiFactory.KindsDirectory).Find(TestApi.Kind)!;

        Assert.Equal("scenarioName", kind.Find("exerciseName")!.Value.Question.Default);
    }

    [Fact]
    public void Choice_recommendation_outside_its_options_fails_at_load()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new KindCatalog(TempKindDir(duplicateIds: false, badChoice: true)));
        Assert.Contains("recommended", ex.Message);
    }

    [Fact]
    public void Duplicate_question_ids_fail_at_load()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new KindCatalog(TempKindDir(duplicateIds: true, badChoice: false)));
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public async Task Kinds_endpoint_lists_and_returns_the_kind()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();

        var list = await client.GetFromJsonAsync<JsonArray>("/api/kinds");
        Assert.Equal("crucible-tabletop", (string)list![0]!["id"]!);

        var kind = await client.GetFromJsonAsync<JsonObject>("/api/kinds/crucible-tabletop");
        var actorType = kind!["stages"]![2]!["questions"]!.AsArray().Single(q => (string)q!["id"]! == "actorType")!;
        Assert.Equal("choice", (string)actorType["answer"]!);

        var missing = await client.GetAsync("/api/kinds/nope");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("KIND_UNKNOWN", (string)(await missing.Content.ReadFromJsonAsync<JsonObject>())!["code"]!);
    }

    /// <summary>A directory holding one small kind, broken in the way asked for.</summary>
    private static string TempKindDir(bool duplicateIds, bool badChoice)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sb-kind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var second = duplicateIds ? "a" : "b";
        var recommended = badChoice ? "z" : "x";
        File.WriteAllText(Path.Combine(dir, "small.kind.json"), $$"""
            {
              "id": "small", "version": "0.0.1", "name": "Small",
              "stages": [
                { "number": 0, "title": "Only", "gate": {}, "questions": [
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
