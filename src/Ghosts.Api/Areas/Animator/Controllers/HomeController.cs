// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using Ghosts.Api;
using ghosts.api.Areas.Animator.Infrastructure.ContentServices.Ollama;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Areas.Animator.Controllers;

[Area("Animator")]
[Route("animator")]
public class HomeController : Controller
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        var x = new OllamaFormatterService(Program.ApplicationSettings.AnimatorSettings.Animations.SocialSharing.ContentEngine);
        var o = await x.GenerateNextAction(new NpcRecord(),"why is the sky blue?");
        return Ok(o);

    }
}