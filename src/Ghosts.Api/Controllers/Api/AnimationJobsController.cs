// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using ghosts.api.Infrastructure.Animations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ghosts.api.Controllers.Api;

[Route("animations")]
[Route("api/[controller]")]
[Produces("application/json")]
public class AnimationJobsController(IServiceProvider serviceProvider) : Controller
{
    private readonly AnimationsManager _animationsManager = serviceProvider.GetRequiredService<IManageableHostedService>() as AnimationsManager;
    //
    // [SwaggerOperation("AnimationStart")]
    // [HttpGet("start")]
    // public async Task<IActionResult> Start(CancellationToken cancellationToken)
    // {
    //     await _animationsManager.StartAsync(cancellationToken);
    //     return Ok();
    // }
    //
    // [SwaggerOperation("AnimationStop")]
    // [HttpGet("stop")]
    // public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    // {
    //     await _animationsManager.StopAsync(cancellationToken);
    //     return Ok();
    // }
    //
    // [SwaggerOperation("AnimationGetStatus")]
    // [HttpGet("status")]
    // public IActionResult Status(CancellationToken cancellationToken)
    // {
    //     return Ok(_animationsManager.GetRunningJobs());
    // }
}
