// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcChatController(INpcChatService service) : ControllerBase
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Returns the chat engine configuration and the models installed on the configured host
    /// </summary>
    [ProducesResponseType(typeof(NpcChatConfig), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(NpcChatConfig))]
    [SwaggerOperation("NpcChatGetConfig")]
    [HttpGet("config")]
    public async Task<ActionResult<NpcChatConfig>> GetConfig(CancellationToken ct)
    {
        return Ok(await service.GetConfig(ct));
    }

    /// <summary>
    /// Chats with an NPC in character (mode "as") or with an assistant about the NPC (mode "about").
    /// The chat can also dispatch a command to the NPC's machine, e.g. "go read the news on cnn.com".
    /// </summary>
    /// <param name="id">NPC id</param>
    /// <param name="request">The operator message, prior turns, mode, and optional model override</param>
    /// <param name="ct">Cancellation token</param>
    [ProducesResponseType(typeof(NpcChatResponse), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(NpcChatResponse))]
    [SwaggerOperation("NpcChatSend")]
    [HttpPost("{id}")]
    public async Task<ActionResult<NpcChatResponse>> Chat(Guid id, [FromBody] NpcChatRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
            return BadRequest(new { error = "A message is required" });

        NpcChatResponse response;
        try
        {
            response = await service.Chat(id, request, ct);
        }
        catch (InvalidOperationException ex)
        {
            // The model host is unreachable, the model is not installed, or it returned nonsense
            _log.Error($"NPC chat failed for {id}: {ex.Message}");
            return StatusCode((int)HttpStatusCode.BadGateway, new { error = ex.Message });
        }

        if (response == null)
            return NotFound(new { error = $"NPC {id} not found" });

        return Ok(response);
    }
}
