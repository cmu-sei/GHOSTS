// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using Ghosts.Api.Infrastructure.Animations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[Route("animations")]
[Route("api/animations")]
[Produces("application/json")]
public class AnimationJobsController(IServiceProvider serviceProvider) : Controller
{
    private readonly AnimationsManager _animationsManager = serviceProvider.GetRequiredService<IManageableHostedService>() as AnimationsManager;

    [SwaggerOperation("AnimationJobsGet")]
    [HttpGet("jobs")]
    public IEnumerable<JobInfo> Get(CancellationToken cancellationToken)
     {
         return _animationsManager.GetRunningJobs();
     }

    [HttpPost("start")]
    public IActionResult Start(AnimationConfiguration configuration, [FromForm] string jobConfiguration)
    {
        configuration.JobConfiguration = jobConfiguration;
        _animationsManager.StartJob(configuration, CancellationToken.None);
        return Ok();
    }

    [HttpPost("stop")]
    public IActionResult Stop(string jobId)
    {
        _animationsManager.StopJob(jobId);
        return Ok();
    }
}
