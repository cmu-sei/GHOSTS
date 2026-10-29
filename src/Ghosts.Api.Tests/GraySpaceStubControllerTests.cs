// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Stubs;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Tests;

/// <summary>
/// The stub's HTTP surface answers with the contract's status codes and error bodies: 404 when the stub is off,
/// 401 for a wrong key, 202 for an accepted build or teardown, 409 for a manifest that is not ready yet.
/// </summary>
public class GraySpaceStubControllerTests
{
    private readonly GraySpaceStub _stub = new(() => DateTime.UtcNow, buildSeconds: 30);

    [Fact]
    public void With_the_stub_off_every_call_is_not_found()
    {
        var result = Controller(enabled: false).ListBases();

        Assert.Equal(404, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void A_wrong_api_key_is_unauthorized()
    {
        var result = Controller(key: "wrong").ListBases();

        var error = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("unauthorized", Assert.IsType<TerrainError>(error.Value).Code);
    }

    [Fact]
    public void An_accepted_build_returns_202_with_the_instance()
    {
        var result = Controller().CreateInstance(new CreateTerrainInstance("office-base", "office-enclave", "k1"));

        var accepted = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(202, accepted.StatusCode);
        Assert.Equal(TerrainStatus.Queued, Assert.IsType<TerrainInstance>(accepted.Value).Status);
    }

    [Fact]
    public void A_manifest_before_the_build_is_ready_is_a_conflict()
    {
        var controller = Controller();
        var created = (TerrainInstance)((ObjectResult)controller.CreateInstance(new CreateTerrainInstance("office-base", "office-enclave", "k2"))).Value!;

        var result = controller.GetManifest(created.Id);

        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void An_unknown_instance_is_not_found()
    {
        var result = Controller().GetInstance("inst-none");

        var error = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("instance_not_found", Assert.IsType<TerrainError>(error.Value).Code);
    }

    private GraySpaceStubController Controller(bool enabled = true, string key = "dev-key")
    {
        var controller = new GraySpaceStubController(_stub, Options.Create(new TerrainProviderOptions { EnableStub = enabled, ApiKey = "dev-key" }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers["X-Api-Key"] = key;
        return controller;
    }
}
