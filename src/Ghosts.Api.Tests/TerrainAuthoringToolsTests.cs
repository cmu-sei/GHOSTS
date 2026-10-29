// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The authoring agent can read what GraySpace offers (the deployed base, images, roles, ISPs, enclave features and
/// stored enclaves) and check a validated draft's terrain as GraySpace would build it, so the terrain it writes is one
/// GraySpace can build. Both tools only read: validate stores nothing.
/// </summary>
public class TerrainAuthoringToolsTests
{
    private readonly GraySpaceSimulator _sim = new(() => DateTime.UtcNow, 30);
    private static readonly TerrainProviderOptions Settings = new() { ClientUrl = "http://ghosts:5000/client.zip", GhostsApiUrl = "http://ghosts:5000/api" };

    private const string Draft = """
        {"schemaVersion":"1.2.0","slug":"office-day","name":"Office Day","description":"An ordinary day at the office.",
         "terrain":{"reference":{"provider":"grayspace","enclave":"office-enclave"},"segments":[{"name":"Office LAN"}],
                    "hosts":[{"name":"desk-01","segment":"Office LAN","os":"ubuntu","role":"workstation"}]},
         "population":{"pools":[{"role":"office staff","count":2,"segments":["Office LAN"]}]}}
        """;

    [Fact]
    public async Task Capabilities_say_what_graySpace_can_build_and_which_enclaves_it_holds()
    {
        await _sim.PutSliceAsync(new SliceSpec("front-office", "fo", "Reception", "ghosts", [new SliceSite("main", new SliceAttach(Isp: "Lumen"), [new SliceSegment("lan")])], []), default);

        var outcome = await Tools(TestDb.New("tools")).RunAsync(ScenarioAuthoringTools.TerrainCapabilities, new JsonObject(), null!, default);

        Assert.True(outcome.Ok, outcome.Text);
        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.Equal("grayspace/2026.10", doc.RootElement.GetProperty("base").GetProperty("version").GetString());
        Assert.Contains("ubuntu-22.04-lxc", doc.RootElement.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("name").GetString()));
        Assert.Contains("ghosts-client", doc.RootElement.GetProperty("roles").EnumerateArray().Select(i => i.GetProperty("name").GetString()));
        Assert.Contains("Lumen", doc.RootElement.GetProperty("isps").EnumerateArray().Select(i => i.GetProperty("name").GetString()));
        Assert.Contains("front-office", doc.RootElement.GetProperty("enclaves").EnumerateArray().Select(i => i.GetProperty("slug").GetString()));
    }

    [Fact]
    public async Task A_validated_drafts_terrain_is_checked_as_graySpace_would_build_it_and_nothing_is_stored()
    {
        await using var context = TestDb.New("tools");
        var session = await Session(context, Draft.Replace("\"os\":\"ubuntu\"", "\"os\":\"windows\""));

        var outcome = await Tools(context).RunAsync(ScenarioAuthoringTools.TerrainValidate, new JsonObject { ["hash"] = "abc123" }, session, default);

        Assert.True(outcome.Ok, outcome.Text);
        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.True(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal("office-enclave", doc.RootElement.GetProperty("spec").GetProperty("slug").GetString());
        Assert.Contains(doc.RootElement.GetProperty("notes").EnumerateArray(), n => n.GetString()!.Contains("windows"));
        Assert.DoesNotContain(await _sim.ListSlicesAsync(default), s => s.Kind == "slice");
    }

    [Fact]
    public async Task GraySpace_findings_come_back_to_the_model()
    {
        await using var context = TestDb.New("tools");
        var session = await Session(context, Draft);

        var outcome = await Tools(context, new TerrainProviderOptions()).RunAsync(ScenarioAuthoringTools.TerrainValidate, new JsonObject { ["hash"] = "abc123" }, session, default);

        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.Contains(doc.RootElement.GetProperty("findings").EnumerateArray(), f => f.GetProperty("code").GetString() == "MISSING_ROLE_VAR");
    }

    [Fact]
    public async Task An_unknown_hash_is_a_tool_error()
    {
        await using var context = TestDb.New("tools");
        var session = await Session(context, Draft);

        var outcome = await Tools(context).RunAsync(ScenarioAuthoringTools.TerrainValidate, new JsonObject { ["hash"] = "nope" }, session, default);

        Assert.False(outcome.Ok);
        Assert.Contains("nope", outcome.Text);
    }

    [Fact]
    public async Task A_graySpace_error_is_a_tool_error_the_model_can_read()
    {
        var graySpace = new Mock<IGraySpace>();
        graySpace.Setup(g => g.GetBaseAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TerrainProviderException(0, "unreachable", "GraySpace at http://gs did not answer"));

        var outcome = await Tools(TestDb.New("tools"), graySpace: graySpace.Object).RunAsync(ScenarioAuthoringTools.TerrainCapabilities, new JsonObject(), null!, default);

        Assert.False(outcome.Ok);
        Assert.Contains("unreachable", outcome.Text);
    }

    [Fact]
    public void Both_terrain_tools_are_offered_to_the_model()
    {
        var names = ScenarioAuthoringTools.Configuration().Tools.Select(t => t.ToolSpec.Name).ToList();

        Assert.Contains(ScenarioAuthoringTools.TerrainCapabilities, names);
        Assert.Contains(ScenarioAuthoringTools.TerrainValidate, names);
    }

    private ScenarioAuthoringTools Tools(ApplicationDbContext context, TerrainProviderOptions? settings = null, IGraySpace? graySpace = null)
    {
        var services = new ServiceCollection().AddSingleton(graySpace ?? _sim).AddSingleton(Options.Create(settings ?? Settings)).BuildServiceProvider();
        return new ScenarioAuthoringTools(context, services.GetRequiredService<IServiceScopeFactory>(), null!, TimeSpan.FromSeconds(5));
    }

    private static async Task<AuthoringSession> Session(ApplicationDbContext context, string document)
    {
        var session = new AuthoringSession { Id = Guid.NewGuid(), Model = "test" };
        context.AuthoringSessions.Add(session);
        context.AuthoringDocuments.Add(new AuthoringDocument { SessionId = session.Id, Hash = "abc123", Document = document });
        await context.SaveChangesAsync();
        return session;
    }
}
