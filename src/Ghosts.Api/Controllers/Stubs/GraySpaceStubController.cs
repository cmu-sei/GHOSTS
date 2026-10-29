// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Stubs;

/// <summary>
/// A stand-in for the GraySpace Terrain API, version 1: the calls GHOSTS will make, answered from memory. It exists
/// so GHOSTS runs end to end without Proxmox, and so the contract can be read and tried in Swagger. It is off unless
/// TerrainProvider:EnableStub is true, as the Aspire AppHost sets it for development.
/// See docs/core/api/terrain.md.
/// </summary>
[ApiController]
[Route("stubs/grayspace/api/v1")]
[SwaggerTag("GraySpace Terrain API v1 (stub). The calls GHOSTS needs from GraySpace.")]
public class GraySpaceStubController(GraySpaceStub stub, IOptions<TerrainProviderOptions> options) : ControllerBase
{
    /// <summary>The bases this provider can build enclaves on.</summary>
    [HttpGet("bases")]
    [ProducesResponseType(typeof(List<TerrainBase>), 200)]
    public IActionResult ListBases() => Guard() ?? Ok(stub.ListBases());

    /// <summary>A base's own segments and hosts, so authoring can name them.</summary>
    [HttpGet("bases/{baseId}/snapshot")]
    [ProducesResponseType(typeof(TerrainBaseSnapshot), 200)]
    [ProducesResponseType(typeof(TerrainError), 404)]
    public IActionResult GetBaseSnapshot(string baseId) =>
        Guard() ?? (stub.Snapshot(baseId) is { } snapshot ? Ok(snapshot) : Error(404, "base_not_found", $"No base {baseId}"));

    /// <summary>The enclaves this provider can build, and the base each needs.</summary>
    [HttpGet("enclaves")]
    [ProducesResponseType(typeof(List<TerrainEnclave>), 200)]
    public IActionResult ListEnclaves() => Guard() ?? Ok(stub.ListEnclaves());

    /// <summary>Build an enclave on a base. Repeating a request with the same idempotency key returns the same instance.</summary>
    [HttpPost("instances")]
    [ProducesResponseType(typeof(TerrainInstance), 202)]
    public IActionResult CreateInstance([FromBody] CreateTerrainInstance request) =>
        Guard() ?? StatusCode(202, stub.Create(request));

    /// <summary>An instance's status, progress and job log.</summary>
    [HttpGet("instances/{id}")]
    [ProducesResponseType(typeof(TerrainInstance), 200)]
    [ProducesResponseType(typeof(TerrainError), 404)]
    public IActionResult GetInstance(string id) =>
        Guard() ?? (stub.Get(id) is { } instance ? Ok(instance) : Error(404, "instance_not_found", $"No instance {id}"));

    /// <summary>Every host in a ready instance, with its placement and use, and the instance's service sites.</summary>
    [HttpGet("instances/{id}/manifest")]
    [ProducesResponseType(typeof(TerrainManifest), 200)]
    [ProducesResponseType(typeof(TerrainError), 404)]
    [ProducesResponseType(typeof(TerrainError), 409)]
    public IActionResult GetManifest(string id)
    {
        if (Guard() is { } denied) return denied;
        var result = stub.Manifest(id);
        if (result.Manifest != null) return Ok(result.Manifest);
        return Error(result.Error.Code == "not_ready" ? 409 : 404, result.Error.Code, result.Error.Message);
    }

    /// <summary>Tear an instance down. The base stays.</summary>
    [HttpDelete("instances/{id}")]
    [ProducesResponseType(typeof(TerrainInstance), 202)]
    [ProducesResponseType(typeof(TerrainError), 404)]
    public IActionResult DeleteInstance(string id) =>
        Guard() ?? (stub.Delete(id) is { } instance ? StatusCode(202, instance) : Error(404, "instance_not_found", $"No instance {id}"));

    private IActionResult Guard()
    {
        if (!options.Value.EnableStub) return Error(404, "stub_disabled", "The GraySpace stub is off (TerrainProvider:EnableStub).");
        var expected = options.Value.ApiKey;
        if (!string.IsNullOrEmpty(expected) && Request.Headers["X-Api-Key"] != expected)
            return Error(401, "unauthorized", "Missing or wrong X-Api-Key.");
        return null;
    }

    private ObjectResult Error(int status, string code, string message) => StatusCode(status, new TerrainError(code, message));
}
