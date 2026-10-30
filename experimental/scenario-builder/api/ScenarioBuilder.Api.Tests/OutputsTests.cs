// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class OutputsTests
{
    private const string Starter = "crucible-tabletop-basics";

    private static Task<HttpResponseMessage> Document(HttpClient client, int id, string template, bool download = false) =>
        client.GetAsync($"/api/scenarios/{id}/documents/{Starter}/{template}{(download ? "?download=true" : "")}");

    private static string? FileName(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

    [Fact]
    public async Task Export_is_the_pull_document_as_an_attachment()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await Put(owner, id, "participants", new { value = "Harbor operators" });

        var export = await owner.GetAsync($"/api/scenarios/{id}/export");

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("harbor-blackout.scenario.json", FileName(export));
        var pulled = await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}");
        Assert.True(JsonNode.DeepEquals(pulled, JsonNode.Parse(await export.Content.ReadAsStringAsync())));
    }

    [Fact]
    public async Task Markdown_export_is_the_whole_scenario_stage_by_stage()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        SetStage(factory, id, 1);
        await Put(owner, id, "participants", new { value = "Harbor operators" });
        await Put(factory.Client(agent: true), id, "headcount", new { value = "Four teams", reason = "Fits the room." });
        await Put(owner, id, "objectives", new { value = new[] { "Assess the cranes", "Practice the handover" } });

        var export = await owner.GetAsync($"/api/scenarios/{id}/export?format=markdown");

        Assert.Equal("text/markdown", export.Content.Headers.ContentType?.MediaType);
        Assert.Equal("harbor-blackout.scenario.md", FileName(export));
        var text = (await export.Content.ReadAsStringAsync()).Replace("\r\n", "\n");
        Assert.StartsWith("> Draft:", text);
        Assert.Contains("\n# Harbor Blackout\n", text);
        Assert.Contains("\n## Stage 0: Engagement Context\n", text);
        Assert.Contains("- **Participants:** Harbor operators\n  _Stated by dev._\n", text);
        Assert.Contains("- **Headcount and Teams:** Four teams\n  _Proposed by the agent. Fits the room._\n", text);
        Assert.Contains("- **Scope Documents:** _Not answered yet._\n", text);
        Assert.Contains("- **Objectives:**\n  - Assess the cranes\n  - Practice the handover\n  _Stated by dev._\n", text);
        Assert.Contains("\n## Open Findings\n", text);
        Assert.Equal("FORMAT_UNKNOWN", await Code(await owner.GetAsync($"/api/scenarios/{id}/export?format=docx")));
    }

    [Fact]
    public async Task Markdown_export_places_drafts_after_their_stage()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 3);

        var text = (await owner.GetStringAsync($"/api/scenarios/{id}/export?format=markdown")).Replace("\r\n", "\n");

        var summary = text.IndexOf("\n### Executive Summary\n\nA summary.\n", StringComparison.Ordinal);
        Assert.True(summary > text.IndexOf("## Stage 2:", StringComparison.Ordinal));
        Assert.True(summary < text.IndexOf("## Stage 3:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Documents_lists_the_starter_templates_for_the_kind()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var documents = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/documents"))!;

        Assert.Equal(["design-summary", "executive-brief", "participant-read-in"], documents.Select(d => (string)d!["template"]!));
        Assert.All(documents, d =>
        {
            Assert.Equal(Starter, (string)d!["library"]!);
            Assert.Equal("Tabletop Exercise (TTX)", (string)d["libraryName"]!);
            Assert.Equal(1, (int)d["version"]!);
        });
        Assert.Equal(["designers", "facilitators", "participants"], documents.Select(d => (string)d!["audience"]!));

        var team = await CreateScenario(owner, "Team Drill", SmallUnitKind);
        var teamDocuments = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{team}/documents"))!;
        Assert.Equal(["team-read-ahead", "controller-guide", "after-action-review"], teamDocuments.Select(d => (string)d!["template"]!));
        Assert.All(teamDocuments, d => Assert.Equal("small-unit-team-exercise", (string)d!["library"]!));
    }

    [Fact]
    public async Task Participant_read_in_leaves_out_the_summary_that_quotes_ground_truth()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 3);
        (await Put(owner, id, "groundTruth", new { value = new[] { "The generators failed on purpose." } })).EnsureSuccessStatusCode();
        (await Draft(owner, id, "Exercise control knows the generators failed on purpose.")).EnsureSuccessStatusCode();

        var readIn = await (await Document(owner, id, "participant-read-in")).Content.ReadAsStringAsync();
        var summary = await (await Document(owner, id, "design-summary")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("generators failed", readIn);
        Assert.Contains("The generators failed on purpose.", summary);
        Assert.Contains("Exercise control knows the generators failed on purpose.", summary);
    }

    [Fact]
    public async Task A_document_gets_only_what_its_audience_may_see_whatever_its_template_asks_for()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 3);
        await Put(owner, id, "groundTruth", new { value = new[] { "GT-MARKER" } });
        await Put(owner, id, "motive", new { value = "MOTIVE-MARKER" });
        await Put(owner, id, "teachingPoints", new { value = new[] { "TP-MARKER" } });
        await Draft(owner, id, "SUMMARY-MARKER");
        const string greedy = "{{ scenario }}|{{ scenario.scenario.executiveSummary }}|{{ scenario.groundTruth }}|{{ scenario.scenario.motive }}" +
                              "|{{ scenario.teachingPoints }}|{{ scenario.scenario.problem }}|D={{ scenario.decisions | size }}|F={{ scenario.findings | size }}";
        var file = new JsonObject
        {
            ["format"] = "scenario-builder-library", ["formatVersion"] = 1, ["slug"] = "greedy", ["name"] = "Greedy",
            ["kinds"] = new JsonArray(Kind), ["version"] = 1,
            ["templates"] = new JsonArray(
                new JsonObject { ["id"] = "players", ["name"] = "Players", ["audience"] = "participants", ["format"] = "markdown", ["body"] = greedy },
                new JsonObject { ["id"] = "control", ["name"] = "Control", ["audience"] = "facilitators", ["format"] = "markdown", ["body"] = greedy })
        };
        (await owner.PostAsync("/api/admin/libraries/import", new StringContent(file.ToJsonString(), Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();

        var players = await owner.GetStringAsync($"/api/scenarios/{id}/documents/greedy/players?download=true");
        var control = await owner.GetStringAsync($"/api/scenarios/{id}/documents/greedy/control?download=true");

        Assert.Contains("Answer for problem", players);
        foreach (var marker in new[] { "GT-MARKER", "MOTIVE-MARKER", "TP-MARKER", "SUMMARY-MARKER" })
            Assert.DoesNotContain(marker, players);
        Assert.Contains("D=0|F=0", players);
        Assert.Contains("GT-MARKER", control);
        Assert.Contains("SUMMARY-MARKER", control);
        Assert.Contains("D=0|F=0", control);
    }

    [Fact]
    public async Task Markdown_download_is_raw_with_the_draft_line()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var download = await Document(owner, id, "executive-brief", download: true);

        Assert.Equal("text/markdown", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("harbor-blackout-executive-brief.md", FileName(download));
        Assert.StartsWith("> Draft:", await download.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Documents_carry_the_content_security_policy()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        foreach (var response in new[] { await Document(owner, id, "design-summary"), await Document(owner, id, "design-summary", download: true) })
        {
            Assert.Equal("default-src 'none'; style-src 'unsafe-inline'; img-src data:", response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        }
    }

    [Fact]
    public async Task Unicode_survives_html_and_markdown()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await Put(owner, id, "participants", new { value = "Données — 記録" });

        Assert.Contains("Données — 記録", await (await Document(owner, id, "design-summary")).Content.ReadAsStringAsync());
        Assert.Contains("Données — 記録", await (await Document(owner, id, "participant-read-in", download: true)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_broken_published_template_gives_an_error_page()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        var file = new JsonObject
        {
            ["format"] = "scenario-builder-library", ["formatVersion"] = 1, ["slug"] = "runaway", ["name"] = "Runaway",
            ["kinds"] = new JsonArray(Kind), ["version"] = 1,
            ["templates"] = new JsonArray(new JsonObject
            {
                ["id"] = "forever", ["name"] = "Forever", ["audience"] = "participants", ["format"] = "html",
                ["body"] = "{% for i in (1..100000) %}x{% endfor %}"
            })
        };
        (await owner.PostAsync("/api/admin/libraries/import", new StringContent(file.ToJsonString(), Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();

        var response = await owner.GetAsync($"/api/scenarios/{id}/documents/runaway/forever");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Forever, line 1", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Document(owner, id, "participant-read-in")).StatusCode);
    }
}
