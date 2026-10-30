// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class StarterLibraryTests
{
    private const string Starter = "crucible-tabletop-basics";
    private const string SmallUnit = "small-unit-team-exercise";


    private static async Task<JsonObject[]> Libraries(HttpClient admin) =>
        (await admin.GetFromJsonAsync<JsonArray>("/api/admin/libraries"))!.Select(l => l!.AsObject()).ToArray();

    [Fact]
    public async Task Starter_libraries_load_into_an_empty_installation()
    {
        using var factory = new ApiFactory();

        var libraries = await Libraries(factory.Client());

        Assert.Equal(["Small Unit Team Exercise", "Tabletop Exercise (TTX)"], libraries.Select(l => (string)l["name"]!));
        Assert.Equal([SmallUnit, Starter], libraries.Select(l => (string)l["slug"]!));
        Assert.All(libraries, l => Assert.Equal(1, (int)l["latestVersion"]!));
        Assert.Equal([[TestApi.SmallUnitKind], [TestApi.Kind]], libraries.Select(l => l["kinds"]!.AsArray().Select(k => (string)k!).ToArray()));
    }

    [Fact]
    public async Task Starter_libraries_load_only_once()
    {
        using var first = new ApiFactory();
        await Libraries(first.Client());

        using var second = new ApiFactory(first.Database);
        var libraries = await Libraries(second.Client());

        Assert.Equal(2, libraries.Length);
        Assert.All(libraries, l => Assert.Equal(1, (int)l["latestVersion"]!));
    }

    [Fact]
    public async Task Every_starter_template_renders_for_a_finished_scenario_of_its_kind()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var finished = new Dictionary<string, JsonObject>();
        foreach (var kindId in new[] { TestApi.Kind, TestApi.SmallUnitKind })
        {
            var kind = KindOf(kindId);
            var id = await CreateScenario(owner, $"Finished {kindId}", kindId);
            await ReachStage(owner, id, kind.LastStage, kind);
            await AnswerStage(owner, id, kind.LastStage, kind);
            finished[kindId] = (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!;
        }
        var files = Directory.GetFiles(ApiFactory.LibrariesDirectory, "*.library.json").Order()
            .Select(path => LibraryFile.Read(File.ReadAllText(path))).ToList();

        Assert.Equal(["design-summary", "executive-brief", "participant-read-in"], files.Single(f => f.Slug == Starter).Templates.Select(t => t.Id));
        Assert.Equal(["team-read-ahead", "controller-guide", "after-action-review"], files.Single(f => f.Slug == SmallUnit).Templates.Select(t => t.Id));
        foreach (var (file, template) in files.SelectMany(f => f.Templates.Select(t => (f, t))))
        {
            var kind = KindOf(file.Kinds.Single());
            var document = finished[kind.Id];
            var input = new RenderInput(AudienceFilter.For(document, kind, template.Audience),
                kind.AllQuestions.ToDictionary(q => q.Id, q => q.Label ?? q.Id), kind, "Harbor Blackout",
                file.Name, 1, document["findings"]!.AsArray().Count, DateTime.UtcNow);
            var text = TemplateRenderer.Render(template, input);
            Assert.Contains("Answer for exerciseName", text);
            Assert.Contains("Answer for exerciseName", TemplateRenderer.Page(template, input, text));
        }
    }
}
