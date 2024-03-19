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
public class AnimationJobsController : Controller
{
    private readonly AnimationsManager _animationsManager;

    public AnimationJobsController(IServiceProvider serviceProvider)
    {
        _animationsManager = serviceProvider.GetRequiredService<IManageableHostedService>() as AnimationsManager;
    }

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

    [SwaggerOperation("animationsOutput")]
    [HttpGet("output")]
    public IActionResult Output(AnimationJobTypes job, CancellationToken cancellationToken)
    {
        var zipFilePath =  _animationsManager.GetOutput(job);
        
        var bytes = System.IO.File.ReadAllBytes(zipFilePath);
        return File(bytes, "application/zip", $"{job.ToString().ToLower()}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.zip");
    }
}