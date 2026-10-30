// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class AdminTests
{
    private static async Task<int> CreateLibrary(HttpClient admin, string name = "Harbor documents")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/libraries", new { name, description = "For tests.", kinds = new[] { Kind } });
        response.EnsureSuccessStatusCode();
        return (int)(await Body(response))["id"]!;
    }

    private static object Template(string body, string id = "brief", string name = "Brief", string audience = "participants", string format = "markdown") =>
        new { id, name, audience, format, body };

    private static Task<HttpResponseMessage> Save(HttpClient admin, int id, params object[] templates) =>
        admin.PutAsJsonAsync($"/api/admin/libraries/{id}", new { name = "Harbor documents", description = "For tests.", kinds = new[] { Kind }, templates });

    private static Task<HttpResponseMessage> Publish(HttpClient admin, int id) =>
        admin.PostAsync($"/api/admin/libraries/{id}/publish", null);

    private static async Task<JsonObject> Listed(HttpClient admin, int id) =>
        (await admin.GetFromJsonAsync<JsonArray>("/api/admin/libraries"))!.Single(l => (int)l!["id"]! == id)!.AsObject();

    private static Task<HttpResponseMessage> Import(HttpClient admin, string file) =>
        admin.PostAsync("/api/admin/libraries/import", new StringContent(file, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task Only_admins_manage_libraries()
    {
        using var factory = new ApiFactory();

        var person = await factory.Client(user: "lee").GetAsync("/api/admin/libraries");
        Assert.Equal(HttpStatusCode.Forbidden, person.StatusCode);
        Assert.Equal("NOT_ADMIN", await Code(person));

        var agent = await factory.Client(agent: true).GetAsync("/api/admin/libraries");
        Assert.Equal(HttpStatusCode.Forbidden, agent.StatusCode);
        Assert.Equal("NOT_ADMIN", await Code(agent));

        Assert.Equal(HttpStatusCode.OK, (await factory.Client().GetAsync("/api/admin/libraries")).StatusCode);
    }

    [Fact]
    public async Task Me_says_who_is_an_admin()
    {
        using var factory = new ApiFactory();

        var dev = (await factory.Client().GetFromJsonAsync<JsonObject>("/api/me"))!;
        Assert.Equal("dev", (string)dev["user"]!);
        Assert.True((bool)dev["admin"]!);
        Assert.False((bool)(await factory.Client(user: "lee").GetFromJsonAsync<JsonObject>("/api/me"))!["admin"]!);
    }

    [Fact]
    public async Task Create_save_and_publish_numbers_versions()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateLibrary(admin);

        (await Save(admin, id, Template("# Brief"))).EnsureSuccessStatusCode();
        Assert.Equal(1, (int)(await Body(await Publish(admin, id)))["number"]!);
        (await Save(admin, id, Template("# Brief, again"))).EnsureSuccessStatusCode();
        Assert.Equal(2, (int)(await Body(await Publish(admin, id)))["number"]!);

        var listed = await Listed(admin, id);
        Assert.Equal(2, (int)listed["latestVersion"]!);
        Assert.False((bool)listed["draftChanged"]!);

        (await Save(admin, id, Template("# Brief, a third time"))).EnsureSuccessStatusCode();
        Assert.True((bool)(await Listed(admin, id))["draftChanged"]!);

        var versions = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/libraries/{id}"))!["versions"]!.AsArray();
        Assert.Equal([1, 2], versions.Select(v => (int)v!["number"]!));
        Assert.All(versions, v => Assert.Equal("dev", (string)v!["publishedBy"]!));
    }

    [Fact]
    public async Task Publishing_an_unchanged_draft_is_refused()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateLibrary(admin);
        await Save(admin, id, Template("# Brief"));
        await Publish(admin, id);

        var again = await Publish(admin, id);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("NOTHING_TO_PUBLISH", await Code(again));
    }

    [Fact]
    public async Task Saving_an_invalid_template_says_where()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateLibrary(admin);

        var response = await Save(admin, id, Template("# Brief\n{% if draft %}", name: "Design summary"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("TEMPLATE_INVALID", (string)body["code"]!);
        Assert.Contains("Design summary, line 2", (string)body["message"]!);
    }

    [Fact]
    public async Task Saving_an_oversized_template_is_refused()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateLibrary(admin);

        var response = await Save(admin, id, Template(new string('x', 200_001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("TEMPLATE_INVALID", await Code(response));
        Assert.Empty((await admin.GetFromJsonAsync<JsonObject>($"/api/admin/libraries/{id}"))!["templates"]!.AsArray());
    }

    [Fact]
    public async Task Export_then_import_round_trips_into_another_installation()
    {
        using var source = new ApiFactory();
        var admin = source.Client();
        var id = await CreateLibrary(admin);
        await Save(admin, id, Template("# Brief"), Template("<h1>{{ scenario.name }}</h1>", "summary", "Design summary", "designers", "html"));
        await Publish(admin, id);

        var export = await admin.GetAsync($"/api/admin/libraries/{id}/export");
        Assert.Equal("harbor-documents.library.json", export.Content.Headers.ContentDisposition?.FileNameStar ?? export.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var file = await export.Content.ReadAsStringAsync();

        using var target = new ApiFactory();
        var other = target.Client();
        var imported = await Import(other, file);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var importedId = (int)(await Body(imported))["id"]!;

        var before = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/libraries/{id}"))!["templates"];
        var after = (await other.GetFromJsonAsync<JsonObject>($"/api/admin/libraries/{importedId}"))!["templates"];
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(1, (int)(await Listed(other, importedId))["latestVersion"]!);
    }

    [Fact]
    public async Task Importing_an_existing_slug_adds_a_version()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateLibrary(admin);
        await Save(admin, id, Template("# Brief"));
        await Publish(admin, id);
        var file = JsonNode.Parse(await admin.GetStringAsync($"/api/admin/libraries/{id}/export"))!;
        file["templates"]![0]!["body"] = "# Brief, from another range";

        var imported = await Import(admin, file.ToJsonString());

        Assert.Equal(id, (int)(await Body(imported))["id"]!);
        Assert.Equal(2, (int)(await Listed(admin, id))["latestVersion"]!);
        var draft = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/libraries/{id}"))!["templates"]![0]!;
        Assert.Equal("# Brief, from another range", (string)draft["body"]!);
    }

    [Fact]
    public async Task Importing_a_bad_file_says_why()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var good = new JsonObject
        {
            ["format"] = "scenario-builder-library", ["formatVersion"] = 1, ["slug"] = "harbor-documents", ["name"] = "Harbor documents",
            ["kinds"] = new JsonArray(Kind), ["version"] = 1,
            ["templates"] = new JsonArray(
                new JsonObject { ["id"] = "brief", ["name"] = "Brief", ["audience"] = "participants", ["format"] = "markdown", ["body"] = "# Brief" })
        };

        var wrongFormat = good.DeepClone();
        wrongFormat["format"] = "word-document";
        var response = await Import(admin, wrongFormat.ToJsonString());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("LIBRARY_INVALID", (string)body["code"]!);
        Assert.Contains("format", (string)body["message"]!);

        var twice = good.DeepClone();
        twice["templates"]!.AsArray().Add(twice["templates"]![0]!.DeepClone());
        Assert.Contains("used twice", (string)(await Body(await Import(admin, twice.ToJsonString())))["message"]!);

        var broken = good.DeepClone();
        broken["templates"]![0]!["body"] = "{% for x in %}";
        Assert.Equal("LIBRARY_INVALID", await Code(await Import(admin, broken.ToJsonString())));

        Assert.Equal("LIBRARY_INVALID", await Code(await Import(admin, "not json")));
    }

    [Fact]
    public async Task Numbers_or_nulls_where_names_belong_are_refused_with_the_reason()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        JsonObject File(JsonNode? audience, JsonNode? format, JsonNode? kind) => new()
        {
            ["format"] = "scenario-builder-library", ["formatVersion"] = 1, ["slug"] = "odd", ["name"] = "Odd",
            ["kinds"] = new JsonArray(kind), ["version"] = 1,
            ["templates"] = new JsonArray(new JsonObject { ["id"] = "brief", ["name"] = "Brief", ["audience"] = audience, ["format"] = format, ["body"] = "# Brief" })
        };

        foreach (var (file, reason) in new[]
                 {
                     (File(7, "markdown", Kind), "audience"),
                     (File("participants", 2, Kind), "format"),
                     (File("participants", "markdown", null), "kind")
                 })
        {
            var response = await Import(admin, file.ToJsonString());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await Body(response);
            Assert.Equal("LIBRARY_INVALID", (string)body["code"]!);
            Assert.Contains(reason, (string)body["message"]!);
        }

        var nullTemplate = File("participants", "markdown", Kind);
        nullTemplate["templates"] = new JsonArray((JsonNode?)null);
        Assert.Equal("LIBRARY_INVALID", await Code(await Import(admin, nullTemplate.ToJsonString())));
        var id = await CreateLibrary(admin);
        var saved = await Save(admin, id, new object?[] { null }!);
        Assert.Equal(HttpStatusCode.BadRequest, saved.StatusCode);

        var scenarioId = await CreateScenario(admin);
        var preview = await admin.PostAsJsonAsync("/api/admin/preview", new { scenarioId, audience = 7, format = "html", body = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
        Assert.Equal("TEMPLATE_INVALID", await Code(preview));
    }

    [Fact]
    public async Task Preview_renders_as_the_audience_would_see_it()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var scenarioId = await CreateScenario(admin);
        SetStage(factory, scenarioId, 2);
        await Put(admin, scenarioId, "problem", new { value = "The harbor lights went out." });
        await Put(admin, scenarioId, "groundTruth", new { value = new[] { "An insider pulled the breaker." } });

        var response = await admin.PostAsJsonAsync("/api/admin/preview", new
        {
            scenarioId, audience = "participants", format = "html",
            body = "{{ scenario.groundTruth }}|{{ scenario.scenario.problem }}"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("default-src 'none'; style-src 'unsafe-inline'; img-src data:",
            response.Headers.GetValues("Content-Security-Policy").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The harbor lights went out.", html);
        Assert.DoesNotContain("insider", html);
    }

    [Fact]
    public async Task Preview_of_a_broken_template_says_where()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var scenarioId = await CreateScenario(admin);

        var response = await admin.PostAsJsonAsync("/api/admin/preview", new
        {
            scenarioId, name = "Read-in", audience = "participants", format = "markdown", body = "# Read-in\n\n{% for x in %}"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("TEMPLATE_INVALID", (string)body["code"]!);
        Assert.Contains("Read-in, line 3", (string)body["message"]!);
    }
}
