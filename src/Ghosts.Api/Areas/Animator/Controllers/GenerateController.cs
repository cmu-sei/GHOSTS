// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Animator.Models;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Swashbuckle.AspNetCore.Annotations;
using Npc = Ghosts.Animator.Npc;

namespace ghosts.api.Areas.Animator.Controllers;

/// <summary>
/// Build entire team of NPCs for a campaign and enclave
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class GenerateController : ControllerBase
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context;

    public GenerateController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all NPCs at the specified level - Campaign, Enclave, or Team
    /// </summary>
    /// <param name="key">campaign, enclave, team</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcProfile>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcProfile>))]
    [SwaggerOperation("getKeys")]
    [HttpGet]
    public IEnumerable<string> GetKeys(string key)
    {
        return key.ToLower() switch
        {
            "campaign" => _context.Npcs.Where(x => x.Campaign != null).Distinct().Select(x=>x.Campaign).ToList(),
            "enclave" => _context.Npcs.Where(x => x.Enclave != null).Distinct().Select(x=>x.Enclave).ToList(),
            "team" => _context.Npcs.Where(x => x.Team != null).Distinct().Select(x=>x.Team).ToList(),
            _ => throw new KeyNotFoundException("Invalid key! Key must be campaign, enclave or team")
        };
    }

    /// <summary>
    /// Create NPCs belonging to a campaign, enclave and team based on configuration
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcProfile>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcProfile>))]
    [SwaggerOperation("createBuild")]
    [HttpPost]
    public IEnumerable<NpcRecord> Create(GenerationConfiguration config, CancellationToken ct)
    {
        var t = new Stopwatch();
        t.Start();
            
        var createdNpcs = new List<NpcRecord>();
        foreach (var enclave in config.Enclaves)
        {
            foreach (var team in enclave.Teams)
            {
                for (var i = 0; i < team.Npcs.Number; i++)
                {
                    var last = t.ElapsedMilliseconds;
                    var branch = team.Npcs.Configuration?.Branch ?? MilitaryUnits.GetServiceBranch();
                    var npc = NpcRecord.TransformToNpc(Npc.Generate(branch));
                    npc.Id = npc.NpcProfile.Id;
                    npc.Team = team.Name;
                    npc.Campaign = config.Campaign;
                    npc.Enclave = enclave.Name;
                    this._context.Npcs.Add(npc);
                    createdNpcs.Add(npc);
                    _log.Trace($"{i} generated in {t.ElapsedMilliseconds - last} ms");
                }
            }
        }
        this._context.SaveChanges();
            
        t.Stop();
        _log.Trace($"{createdNpcs.Count} NPCs generated in {t.ElapsedMilliseconds} ms");

        return createdNpcs;
    }
    
    /// <summary>
    /// Generate random NPC by random service branch
    /// </summary>
    /// <returns>NPC Profile</returns>
    [ProducesResponseType(typeof(NpcProfile), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(NpcProfile))]
    [SwaggerOperation("generateNpc")]
    [HttpPost("one")]
    public async Task<NpcRecord> GenerateOne()
    {
        var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
        npc.Id = npc.NpcProfile.Id;
        this._context.Npcs.Add(npc);
        await this._context.SaveChangesAsync();
        return npc;
    }
}