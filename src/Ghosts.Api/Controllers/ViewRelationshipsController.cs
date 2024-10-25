// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Text;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Controllers;

[Route("view-relationships")]
[Controller]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewRelationshipsController(ApplicationDbContext context) : Controller
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet]
    public IActionResult Index()
    {
        return View("Index");
    }

    [HttpGet("profile/{id:guid}")]
    public IActionResult Profile(Guid id)
    {
        var npc = _context.Npcs.FirstOrDefault(x => x.Id == id);
        return View("Profile", npc);
    }

    [HttpGet("files/data1.csv")]
    public FileResult Download()
    {
        const string fileName = "data1.csv";
        var content = new StringBuilder("npc_id,source,target,type").Append(Environment.NewLine);

        var list = _context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o => o.Team);

        NpcRecord previousNpc = null;
        var enclave = string.Empty;
        var team = string.Empty;
        var campaign = string.Empty;

        foreach (var npc in list)
        {
            if (previousNpc == null)
            {
                campaign = npc.Campaign;
            }

            if (previousNpc == null || previousNpc.Enclave != npc.Enclave)
            {
                enclave = npc.Enclave;
                content.Append(',').Append(campaign).Append(',').Append(enclave).Append(",CAMPAIGN").Append(Environment.NewLine);
            }

            if (string.IsNullOrEmpty(team) || previousNpc?.Team != npc.Team)
            {
                team = $"{enclave}/{npc.Team}";
                content.Append(',').Append(enclave).Append(',').Append(team).Append(",TEAM").Append(Environment.NewLine);
            }

            content.Append(npc.Id).Append(',').Append(team).Append(',').Append(npc.NpcProfile.Name).Append(',').Append(npc.Enclave).Append('-').Append(npc.Team).Append(Environment.NewLine);
            previousNpc = npc;
        }
        var fileBytes = Encoding.ASCII.GetBytes(content.ToString());
        return File(fileBytes, "text/csv", fileName);
    }
}
