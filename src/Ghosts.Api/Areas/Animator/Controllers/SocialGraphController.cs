// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator.Models;
using ghosts.api.Areas.Animator.Infrastructure.Animations;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Areas.Animator.Controllers;

[Area("Animator")]
[Route("animator/[controller]")]
[Controller]
[Produces("application/json")]
public class SocialGraphController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AnimationsManager _animationsManager;
        
    public SocialGraphController(ApplicationDbContext context, AnimationsManager animationsManager)
    {
        this._context = context;
        _animationsManager = animationsManager;
    }

    [SwaggerOperation("startSocialGraph")]
    [HttpGet("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        await _animationsManager.StartAsync(cancellationToken);
        return Ok();
    }
    
    [SwaggerOperation("stopSocialGraph")]
    [HttpGet("stop")]
    public async Task<IActionResult> stop(CancellationToken cancellationToken)
    {
        await _animationsManager.StopAsync(cancellationToken);
        return Ok();
    }
    
    /// <summary>
    /// Get NPC's social graph by Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(SocialGraph), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(NpcProfile))]
    [SwaggerOperation("getSocialGraphById")]
    [HttpGet("{id:guid}")]
    public SocialGraph GetById(Guid id)
    {
        var graph = new SocialGraph
        {
            Id = id
        };
        var list = this._context.Npcs.Include(npcProfile => npcProfile.NpcProfile.Name).ToList().OrderBy(o => o.Enclave).ThenBy(o=>o.Team);
        
        NpcRecord previousNpc = null;
        var enclave = string.Empty;
        var team = string.Empty;
            
        foreach (var npc in list)
        {
            var connection = new SocialGraph.SocialConnection
            {
                Id = npc.Id,
                Name = npc.NpcProfile.Name.ToString()
            };

            if (previousNpc == null)
            {
                connection.Distance = npc.Campaign;
            }
            else if (previousNpc.Enclave != npc.Enclave)
            {
                enclave = npc.Enclave;
                connection.Distance = npc.Enclave;
                continue;
            }
            else if (string.IsNullOrEmpty(team) || previousNpc.Team != npc.Team)
            {
                team = $"{enclave}/{npc.Team}";
                connection.Distance = team;
                continue;
            }

            connection.Distance = team;
            graph.Connections.Add(connection);
            previousNpc = npc;
        }

        return graph;
    }
}