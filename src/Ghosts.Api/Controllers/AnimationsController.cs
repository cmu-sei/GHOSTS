// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using Ghosts.Api.Infrastructure.Animations;
using Ghosts.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Ghosts.Api.Controllers;

[Controller]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AnimationsController(IManageableHostedService animationsManager) : Controller
{
    private readonly ApplicationSettings _configuration = Program.ApplicationSettings;

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.FullAutonomy = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.FullAutonomy);
        ViewBag.SocialSharing = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialSharing);
        ViewBag.SocialBelief = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialBelief);
        ViewBag.Chat = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.Chat);
        ViewBag.SocialGraph = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialGraph);

        ViewBag.RunningJobs = animationsManager.GetRunningJobs();
        return View(new AnimationConfiguration());
    }

    [HttpPost("start")]
    public IActionResult Start(AnimationConfiguration configuration, [FromForm] string jobConfiguration)
    {
        configuration.JobConfiguration = jobConfiguration;
        animationsManager.StartJob(configuration, CancellationToken.None);
        return RedirectToAction("Index");
    }

    [HttpPost("stop")]
    public IActionResult Stop(string jobId)
    {
        animationsManager.StopJob(jobId);
        return RedirectToAction("Index");
    }
}
