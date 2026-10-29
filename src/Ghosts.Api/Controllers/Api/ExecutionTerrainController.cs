// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// An execution's terrain: the enclave the terrain provider (GraySpace) builds for the run. Build it, follow it,
/// and tear it down. Status and manifest refresh in the background every few seconds while a build is in progress.
/// </summary>
[ApiController]
[Route("api/executions/{executionId:int}/terrain")]
public class ExecutionTerrainController(ITerrainService service) : ControllerBase
{
    /// <summary>The run's terrain: provider, base, enclave, instance, status, progress, job log and, once ready, the manifest.</summary>
    [HttpGet]
    public Task<ActionResult<ExecutionTerrainDto>> Get(int executionId, CancellationToken ct) =>
        Handle(() => service.GetAsync(executionId, ct));

    /// <summary>Ask the provider to build the run's enclave. Base and enclave default to the scenario's terrain reference.</summary>
    [HttpPost]
    public Task<ActionResult<ExecutionTerrainDto>> Build(int executionId, [FromBody] BuildTerrainDto dto, CancellationToken ct) =>
        Handle(() => service.BuildAsync(executionId, dto ?? new BuildTerrainDto(null, null), ct));

    /// <summary>Ask the provider to tear the run's enclave down. The base stays.</summary>
    [HttpDelete]
    public Task<ActionResult<ExecutionTerrainDto>> Teardown(int executionId, CancellationToken ct) =>
        Handle(() => service.TeardownAsync(executionId, ct));

    /// <summary>Interim export for Crucible Player: the ready manifest's participant consoles. 409 until the terrain is ready.</summary>
    [HttpGet("player-view")]
    public Task<ActionResult<PlayerViewDto>> PlayerView(int executionId, CancellationToken ct) =>
        Handle(() => service.PlayerViewAsync(executionId, ct));

    private async Task<ActionResult<T>> Handle<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (TerrainProviderException ex)
        {
            return StatusCode(502, new { error = ex.Message, providerStatus = ex.Status, providerCode = ex.Code });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
