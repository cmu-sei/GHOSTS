// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>The terrain endpoints say whose problem a failure is: 404 no such run, 409 a refused build, 502 the provider's error.</summary>
public class ExecutionTerrainControllerTests
{
    private readonly Mock<ITerrainService> _service = new();

    [Fact]
    public async Task An_unknown_execution_is_not_found()
    {
        _service.Setup(s => s.BuildAsync(9, It.IsAny<BuildTerrainDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Execution not found"));

        var result = await new ExecutionTerrainController(_service.Object).Build(9, new BuildTerrainDto(null, null), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task A_refused_build_is_a_conflict()
    {
        _service.Setup(s => s.BuildAsync(1, It.IsAny<BuildTerrainDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Execution 1 already has terrain (ready). Tear it down first."));

        var result = await new ExecutionTerrainController(_service.Object).Build(1, new BuildTerrainDto(null, null), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task The_map_is_passed_through_as_geojson()
    {
        _service.Setup(s => s.MapAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync("""{"type":"FeatureCollection","features":[]}""");

        var result = await new ExecutionTerrainController(_service.Object).Map(1, default);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(("application/json", """{"type":"FeatureCollection","features":[]}"""), (content.ContentType, content.Content));
    }

    [Fact]
    public async Task A_provider_error_is_a_bad_gateway_carrying_the_providers_code()
    {
        _service.Setup(s => s.BuildAsync(1, It.IsAny<BuildTerrainDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TerrainProviderException(503, "node_busy", "pve-01 is at capacity"));

        var result = await new ExecutionTerrainController(_service.Object).Build(1, new BuildTerrainDto(null, null), default);

        var error = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(502, error.StatusCode);
        Assert.Contains("node_busy", System.Text.Json.JsonSerializer.Serialize(error.Value));
    }
}
