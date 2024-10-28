// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using ghosts.api.Infrastructure.Animations;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AnimationsController(IManageableHostedService animationsManager) : Controller
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration = Program.ApplicationSettings;
    private readonly IManageableHostedService _animationsManager = animationsManager;

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.FullAutonomy = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.FullAutonomy);
        ViewBag.SocialSharing = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialSharing);
        ViewBag.SocialBelief = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialBelief);
        ViewBag.Chat = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.Chat);
        ViewBag.SocialGraph = JsonConvert.SerializeObject(_configuration.AnimatorSettings.Animations.SocialGraph);

        ViewBag.RunningJobs = _animationsManager.GetRunningJobs();
        return View(new AnimationConfiguration());
    }

    [HttpPost("start")]
    public IActionResult Start(AnimationConfiguration configuration, [FromForm] string jobConfiguration)
    {
        configuration.JobConfiguration = jobConfiguration;
        _animationsManager.StartJob(configuration, new CancellationToken());
        return RedirectToAction("Index");
    }

    [HttpPost("stop")]
    public IActionResult Stop(string jobId)
    {
        _animationsManager.StopJob(jobId);
        return RedirectToAction("Index");
    }
}
