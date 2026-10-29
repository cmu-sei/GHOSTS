// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

public record SetHostDto(string HostName);

/// <summary>
/// Which host each of a run's NPCs sits at. Auto-binding uses the terrain manifest's client hosts (or the document's
/// hosts before the terrain is ready) and each pool's placement; a binding can be changed by hand. The accounts list
/// says who sits where, for whoever creates their accounts on the hosts.
/// </summary>
[ApiController]
[Route("api/executions/{executionId:int}")]
public class ExecutionBindingController(IExecutionBindingService service) : ControllerBase
{
    /// <summary>Each NPC in the run, its pool, its host and its machine once a client with that host name registers.</summary>
    [HttpGet("npcs")]
    public async Task<ActionResult<List<NpcBindingDto>>> List(int executionId, CancellationToken ct) =>
        Ok(await service.ListAsync(executionId, ct));

    /// <summary>Bind every unbound NPC to a free host, by its pool's placement.</summary>
    [HttpPost("npcs/autobind")]
    public async Task<ActionResult<BindingResultDto>> AutoBind(int executionId, CancellationToken ct)
    {
        try { return Ok(await service.AutoBindAsync(executionId, ct)); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Put one NPC at a host by name, or unbind it with an empty host name.</summary>
    [HttpPut("npcs/{npcId:guid}/host")]
    public async Task<IActionResult> SetHost(int executionId, Guid npcId, [FromBody] SetHostDto dto, CancellationToken ct)
    {
        try
        {
            await service.SetHostAsync(executionId, npcId, dto?.HostName, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Each bound NPC's name, username, email, pool and host. No passwords.</summary>
    [HttpGet("accounts")]
    public async Task<ActionResult<List<NpcAccountDto>>> Accounts(int executionId, CancellationToken ct) =>
        Ok(await service.AccountsAsync(executionId, ct));
}
