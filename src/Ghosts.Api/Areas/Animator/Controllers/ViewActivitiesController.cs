using System;
using System.Linq;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Areas.Animator.Controllers;

[Controller]
[Produces("application/json")]
[Area("Animator")]
[Route("animator/view-activities")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
        
    public ViewActivitiesController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpGet]
    public IActionResult Index()
    {
        var list = this._context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o=>o.Team);
        return View("Index", list);
    }
    
    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var o = this._context.Npcs.FirstOrDefault(x => x.Id == id);
        return View("Detail", o);
    }
}