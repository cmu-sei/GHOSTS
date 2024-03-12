// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.Animations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Areas.Animator.Controllers.Api;

[Area("Animator")]
[Route("animations")]
[Controller]
[Produces("application/json")]
public class AnimationJobsController(IServiceProvider serviceProvider) : Controller
{
    private readonly AnimationsManager _animationsManager = serviceProvider.GetRequiredService<IManageableHostedService>() as AnimationsManager;

    [SwaggerOperation("animationsStart")]
    [HttpGet("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        await _animationsManager.StartAsync(cancellationToken);
        return Ok();
    }

    [SwaggerOperation("animationsStop")]
    [HttpGet("stop")]
    public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    {
        await _animationsManager.StopAsync(cancellationToken);
        return Ok();
    }

    [SwaggerOperation("animationsStatus")]
    [HttpGet("status")]
    public IActionResult Status(CancellationToken cancellationToken)
    {
        return Ok(_animationsManager.GetRunningJobs());
    }

    // /// <summary>
    // /// Get NPCs social graph by Id
    // /// </summary>
    // /// <param name="id"></param>
    // /// <returns></returns>
    // [ProducesResponseType(typeof(SocialGraph), (int) HttpStatusCode.OK)]
    // [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(NpcProfile))]
    // [SwaggerOperation("getSocialGraphById")]
    // [HttpGet("{id:guid}")]
    // public SocialGraph GetById(Guid id)
    // {
    //     var graph = new SocialGraph
    //     {
    //         Id = id
    //     };
    //     var list = this._context.Npcs.Include(npcProfile => npcProfile.NpcProfile.Name).ToList().OrderBy(o => o.Enclave).ThenBy(o=>o.Team);
    //     
    //     NpcRecord previousNpc = null;
    //     var enclave = string.Empty;
    //     var team = string.Empty;
    //         
    //     foreach (var npc in list)
    //     {
    //         var connection = new SocialGraph.SocialConnection
    //         {
    //             Id = npc.Id,
    //             Name = npc.NpcProfile.Name.ToString()
    //         };
    //
    //         if (previousNpc == null)
    //         {
    //             connection.Distance = npc.Campaign;
    //         }
    //         else if (previousNpc.Enclave != npc.Enclave)
    //         {
    //             enclave = npc.Enclave;
    //             connection.Distance = npc.Enclave;
    //             continue;
    //         }
    //         else if (string.IsNullOrEmpty(team) || previousNpc.Team != npc.Team)
    //         {
    //             team = $"{enclave}/{npc.Team}";
    //             connection.Distance = team;
    //             continue;
    //         }
    //
    //         connection.Distance = team;
    //         graph.Connections.Add(connection);
    //         previousNpc = npc;
    //     }
    //
    //     return graph;
    // }
}