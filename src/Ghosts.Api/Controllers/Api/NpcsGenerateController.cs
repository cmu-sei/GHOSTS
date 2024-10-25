// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Controllers.Api;

/// <summary>
/// Build entire team of NPCs for a campaign and enclave
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcsGenerateController(INpcService service) : ControllerBase
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly INpcService _service = service;

    /// <summary>
    /// Returns all NPCs at the specified level - Campaign, Enclave, or Team
    /// </summary>
    /// <param name="key">campaign, enclave, team</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<string>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<string>>))]
    [SwaggerOperation("NpcsGenerateGetByKey")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> GetKeys(string key)
    {
        return Ok(await _service.GetKeys(key));
    }

    /// <summary>
    /// Create NPCs belonging to a campaign, enclave and team based on configuration
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcRecord>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcRecord>>))]
    [SwaggerOperation("NpcsGenerateCreate")]
    [HttpPost]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> Create(GenerationConfiguration config, CancellationToken ct)
    {
        if (config == null || config.Enclaves == null)
        {
            return BadRequest(ModelState);
        }

        return Ok(await _service.Create(config, ct));
    }

    /// <summary>
    /// Generate random NPC by random service branch
    /// </summary>
    /// <returns>NPC Profile</returns>
    [ProducesResponseType(typeof(ActionResult<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcRecord>))]
    [SwaggerOperation("NpcsGenerateCreateOne")]
    [HttpPost("one")]
    public async Task<ActionResult<NpcRecord>> CreateOne()
    {
        return await _service.CreateOne();
    }

    /// <summary>
    /// Ensures an NPC is created for each and every machine currentusername that exists
    /// </summary>
    /// <returns>NPC Profile</returns>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerOperation("NpcsGenerateSyncWithMachineUsernames")]
    [HttpPost("syncWithMachineUsernames")]
    public async Task SyncWithMachineUsernames()
    {
        await _service.SyncWithMachineUsernames();
    }
}
