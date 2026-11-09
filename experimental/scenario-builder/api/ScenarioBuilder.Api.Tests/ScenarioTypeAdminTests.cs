// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class ScenarioTypeAdminTests
{
    private static async Task<JsonObject> CreateScenarioType(HttpClient admin, string name = "Red Team Exercise", string? basedOn = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/scenario-types", new { name, description = "A small team attacks a mock enterprise.", topology = true, basedOn });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Body(response);
    }

    /// <summary>A complete scenario type of one stage and one question, as the JSON view would save it.</summary>
    private static object OneStage(string id, string text = "What does the team attack?", string answer = "text") => new
    {
        id, version = "0.0.0", name = "Red Team Exercise", description = "A small team attacks a mock enterprise.", topology = true, drafts = Array.Empty<object>(),
        stages = new[]
        {
            new { number = 0, title = "Target", questions = new[] { new { id = "target", label = "Target", text, why = "Everything aims at it.", answer, path = "/scenario/target" } } }
        }
    };

    private static Task<HttpResponseMessage> Save(HttpClient admin, string id, object scenarioType) => admin.PutAsJsonAsync($"/api/admin/scenario-types/{id}", scenarioType);

    private static Task<HttpResponseMessage> Publish(HttpClient admin, string id) => admin.PostAsync($"/api/admin/scenario-types/{id}/publish", null);

    private static async Task<JsonObject> Listed(HttpClient admin, string id) =>
        (await admin.GetFromJsonAsync<JsonArray>("/api/admin/scenario-types"))!.Single(k => (string)k!["id"]! == id)!.AsObject();

    private static async Task<string[]> Offered(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonArray>("/api/scenario-types"))!.Select(k => (string)k!["id"]!).ToArray();

    /// <summary>Creates, saves and publishes the one-stage red team scenario type.</summary>
    private static async Task<string> Published(HttpClient admin, string text = "What does the team attack?")
    {
        var id = (string)(await CreateScenarioType(admin))["id"]!;
        (await Save(admin, id, OneStage(id, text))).EnsureSuccessStatusCode();
        (await Publish(admin, id)).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task Only_admins_manage_scenario_types()
    {
        using var factory = new ApiFactory();

        var person = await factory.Client(user: "lee").GetAsync("/api/admin/scenario-types");
        Assert.Equal(HttpStatusCode.Forbidden, person.StatusCode);
        Assert.Equal("NOT_ADMIN", await Code(person));
        var agent = await factory.Client(agent: true).PostAsJsonAsync("/api/admin/scenario-types", new { name = "Mine" });
        Assert.Equal(HttpStatusCode.Forbidden, agent.StatusCode);
        Assert.Equal("NOT_ADMIN", await Code(agent));
    }

    [Fact]
    public async Task The_kind_files_start_out_published()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();

        var tabletop = await Listed(admin, TabletopType);
        Assert.Equal("Tabletop Exercise (TTX)", (string)tabletop["name"]!);
        Assert.Equal("0.3.0", (string)tabletop["latestVersion"]!);
        Assert.False((bool)tabletop["draftChanged"]!);
        Assert.False((bool)tabletop["retired"]!);
        Assert.Equal(0, (int)tabletop["scenarios"]!);
        Assert.Equal([TabletopType, SmallUnitType], await Offered(admin));
    }

    [Fact]
    public async Task A_new_kind_starts_as_a_copy_and_is_offered_only_once_published()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();

        var created = await CreateScenarioType(admin, basedOn: TabletopType);

        Assert.Equal("red-team-exercise", (string)created["id"]!);
        Assert.Equal(TabletopType, (string)created["basedOn"]!);
        Assert.Null(created["latestVersion"]);
        var draft = created["scenarioType"]!;
        Assert.Equal("Red Team Exercise", (string)draft["name"]!);
        Assert.Equal("A small team attacks a mock enterprise.", (string)draft["description"]!);
        Assert.True((bool)draft["topology"]!);
        Assert.Equal(5, draft["stages"]!.AsArray().Count);
        Assert.DoesNotContain("red-team-exercise", await Offered(admin));
        var scenario = await admin.PostAsJsonAsync("/api/scenarios", new { name = "Early", scenarioTypeId = "red-team-exercise" });
        Assert.Equal("SCENARIO_TYPE_UNKNOWN", await Code(scenario));

        var blank = await CreateScenarioType(admin, name: "Blank Exercise");
        Assert.Empty(blank["scenarioType"]!["stages"]!.AsArray());
    }

    [Fact]
    public async Task Publishing_bumps_the_minor_version_and_refuses_when_nothing_changed()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await Published(admin);

        Assert.Equal("0.1.0", (string)(await Listed(admin, id))["latestVersion"]!);
        Assert.Contains(id, await Offered(admin));
        var again = await Publish(admin, id);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("NOTHING_TO_PUBLISH", await Code(again));

        (await Save(admin, id, OneStage("someone-else", "What does the team attack first?"))).EnsureSuccessStatusCode();
        Assert.True((bool)(await Listed(admin, id))["draftChanged"]!);
        var second = await Publish(admin, id);
        Assert.Equal("0.2.0", (string)(await Body(second))["version"]!);
        var detail = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/scenario-types/{id}"))!;
        Assert.Equal(id, (string)detail["scenarioType"]!["id"]!);
        Assert.Equal(["0.1.0", "0.2.0"], detail["versions"]!.AsArray().Select(v => (string)v!["version"]!));
    }

    [Fact]
    public async Task A_scenario_stays_on_the_kind_version_it_was_created_with()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await Published(admin);
        var early = await CreateScenario(admin, "Early", id);

        (await Save(admin, id, OneStage(id, "Which network does the team attack?"))).EnsureSuccessStatusCode();
        (await Publish(admin, id)).EnsureSuccessStatusCode();
        var late = await CreateScenario(admin, "Late", id);

        var earlyProgress = await Progress(admin, early);
        Assert.Equal("0.1.0", (string)earlyProgress["scenarioTypeVersion"]!);
        Assert.Equal("What does the team attack?", (string)earlyProgress["open"]![0]!["text"]!);
        Assert.Equal("Which network does the team attack?", (string)(await Progress(admin, late))["open"]![0]!["text"]!);
        var old = (await admin.GetFromJsonAsync<JsonObject>($"/api/scenario-types/{id}?version=0.1.0"))!;
        Assert.Equal("What does the team attack?", (string)old["stages"]![0]!["questions"]![0]!["text"]!);
        Assert.Equal("0.2.0", (string)(await admin.GetFromJsonAsync<JsonObject>($"/api/scenario-types/{id}"))!["version"]!);
        Assert.Equal("SCENARIO_TYPE_UNKNOWN", await Code(await admin.GetAsync($"/api/scenario-types/{id}?version=9.9.9")));
        var versions = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/scenario-types/{id}"))!["versions"]!.AsArray();
        Assert.Equal([1, 1], versions.Select(v => (int)v!["scenarios"]!));
    }

    [Fact]
    public async Task Save_fills_in_what_the_outline_leaves_out()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = (string)(await CreateScenarioType(admin))["id"]!;
        var outline = new
        {
            name = "Red Team Exercise", drafts = Array.Empty<object>(),
            stages = new object[]
            {
                new { title = "Target", questions = new object[]
                {
                    new { text = "What does the team attack first?", answer = "text" },
                    new { text = "What does the team attack first?", answer = "list" }
                } }
            }
        };

        var saved = await Save(admin, id, outline);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var stage = (await Body(saved))["scenarioType"]!["stages"]![0]!;
        Assert.Equal(0, (int)stage["number"]!);
        var questions = stage["questions"]!.AsArray();
        Assert.Equal(["teamAttackFirst", "teamAttackFirst2"], questions.Select(q => (string)q!["id"]!));
        Assert.Equal("Team Attack First", (string)questions[0]!["label"]!);
        Assert.Equal("/scenario/teamAttackFirst", (string)questions[0]!["path"]!);
        Assert.Equal("", (string)questions[0]!["why"]!);
    }

    [Fact]
    public async Task Save_refuses_a_broken_kind_and_publish_refuses_an_unfinished_one()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = (string)(await CreateScenarioType(admin))["id"]!;

        var broken = await Save(admin, id, OneStage(id, answer: "choice"));
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        var error = await Body(broken);
        Assert.Equal("SCENARIO_TYPE_INVALID", (string)error["code"]!);
        Assert.Contains("'target'", (string)error["message"]!);

        var unfinished = await Publish(admin, id);
        Assert.Equal(HttpStatusCode.BadRequest, unfinished.StatusCode);
        Assert.Equal("SCENARIO_TYPE_INVALID", await Code(unfinished));
    }

    [Fact]
    public async Task A_retired_kind_isnt_offered_but_its_scenarios_carry_on()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var scenario = await CreateScenario(admin);

        (await admin.PutAsJsonAsync($"/api/admin/scenario-types/{TabletopType}/retired", new { retired = true })).EnsureSuccessStatusCode();

        Assert.DoesNotContain(TabletopType, await Offered(admin));
        Assert.True((bool)(await Listed(admin, TabletopType))["retired"]!);
        Assert.Equal(1, (int)(await Listed(admin, TabletopType))["scenarios"]!);
        Assert.Equal("SCENARIO_TYPE_RETIRED", await Code(await admin.PostAsJsonAsync("/api/scenarios", new { name = "Too late", scenarioTypeId = TabletopType })));
        (await Put(admin, scenario, "intent", new { value = "Practise a port outage." })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/admin/scenario-types/{TabletopType}/retired", new { retired = false })).EnsureSuccessStatusCode();
        Assert.Contains(TabletopType, await Offered(admin));
    }

    [Fact]
    public async Task Only_a_kind_never_published_can_be_deleted()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = (string)(await CreateScenarioType(admin))["id"]!;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/scenario-types/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/scenario-types/{id}")).StatusCode);
        var seeded = await admin.DeleteAsync($"/api/admin/scenario-types/{TabletopType}");
        Assert.Equal(HttpStatusCode.Conflict, seeded.StatusCode);
        Assert.Equal("SCENARIO_TYPE_PUBLISHED", await Code(seeded));
    }

    [Fact]
    public async Task A_kind_exports_to_a_file_that_imports_into_another_installation()
    {
        using var first = new ApiFactory();
        var admin = first.Client();
        var id = await Published(admin);
        Assert.Equal("NOT_PUBLISHED", await Code(await admin.GetAsync($"/api/admin/scenario-types/{(string)(await CreateScenarioType(admin, "Unpublished"))["id"]!}/export")));

        var export = await admin.GetAsync($"/api/admin/scenario-types/{id}/export");
        Assert.Equal($"{id}.scenario-type.json", export.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var file = await export.Content.ReadAsStringAsync();

        using var second = new ApiFactory();
        var other = second.Client();
        var imported = await other.PostAsync("/api/admin/scenario-types/import", new StringContent(file, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal("0.1.0", (string)(await Listed(other, id))["latestVersion"]!);
        Assert.Contains(id, await Offered(other));
        (await other.PostAsync("/api/admin/scenario-types/import", new StringContent(file, Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();
        Assert.Equal("0.2.0", (string)(await Listed(other, id))["latestVersion"]!);

        var junk = await other.PostAsync("/api/admin/scenario-types/import", new StringContent("""{ "id": "x" }""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, junk.StatusCode);
        Assert.Equal("SCENARIO_TYPE_INVALID", await Code(junk));
    }
}
