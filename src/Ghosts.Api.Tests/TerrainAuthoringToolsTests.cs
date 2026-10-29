// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The authoring agent can read the terrain provider: which bases and enclaves exist, and a base's segments and
/// hosts, so a draft names real hosts instead of inventing them. Both tools only read.
/// </summary>
public class TerrainAuthoringToolsTests
{
    private readonly Mock<ITerrainProvider> _provider = new();

    [Fact]
    public async Task Listing_terrain_returns_the_providers_bases_and_enclaves()
    {
        _provider.Setup(p => p.ListBasesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new TerrainBase("office-base", "Office base", "2", "pve-01", "running")]);
        _provider.Setup(p => p.ListEnclavesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new TerrainEnclave("office-enclave", "1", "office-base", 5)]);

        var outcome = await Tools().RunAsync(ScenarioAuthoringTools.TerrainList, new JsonObject(), null!, default);

        Assert.True(outcome.Ok);
        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.Equal("office-base", doc.RootElement.GetProperty("bases")[0].GetProperty("id").GetString());
        Assert.Equal("office-enclave", doc.RootElement.GetProperty("enclaves")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_bases_snapshot_returns_its_segments_and_hosts()
    {
        _provider.Setup(p => p.GetBaseSnapshotAsync("office-base", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerrainBaseSnapshot("office-base", "2", [new TerrainSegment("Base internet")], [new TerrainHost("mail-01", "Base internet", "linux", "mail")]));

        var outcome = await Tools().RunAsync(ScenarioAuthoringTools.TerrainSnapshot, new JsonObject { ["baseId"] = "office-base" }, null!, default);

        Assert.True(outcome.Ok);
        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.Equal("mail-01", doc.RootElement.GetProperty("hosts")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_provider_error_is_a_tool_error_the_model_can_read()
    {
        _provider.Setup(p => p.GetBaseSnapshotAsync("nope", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TerrainProviderException(404, "base_not_found", "No base nope"));

        var outcome = await Tools().RunAsync(ScenarioAuthoringTools.TerrainSnapshot, new JsonObject { ["baseId"] = "nope" }, null!, default);

        Assert.False(outcome.Ok);
        Assert.Contains("base_not_found", outcome.Text);
    }

    [Fact]
    public void Both_terrain_tools_are_offered_to_the_model()
    {
        var names = ScenarioAuthoringTools.Configuration().Tools.Select(t => t.ToolSpec.Name).ToList();

        Assert.Contains(ScenarioAuthoringTools.TerrainList, names);
        Assert.Contains(ScenarioAuthoringTools.TerrainSnapshot, names);
    }

    private ScenarioAuthoringTools Tools()
    {
        var services = new ServiceCollection().AddSingleton(_provider.Object).BuildServiceProvider();
        return new ScenarioAuthoringTools(null!, services.GetRequiredService<IServiceScopeFactory>(), null!, TimeSpan.FromSeconds(5));
    }
}
