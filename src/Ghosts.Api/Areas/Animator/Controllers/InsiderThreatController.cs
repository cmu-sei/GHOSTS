// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FileHelpers;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Areas.Animator.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class InsiderThreatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
        
    public InsiderThreatController(ApplicationDbContext context)
    {
        this._context = context;
    }
        
    /// <summary>
    /// Create an insider threat specific NPC build
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerRequestExample(typeof(InsiderThreatGenerationConfiguration), typeof(InsiderThreatGenerationConfigurationExample))]
    [SwaggerOperation("createInsiderThreatBuild")]
    [HttpPost]
    public IEnumerable<NpcRecord> Create(InsiderThreatGenerationConfiguration config, CancellationToken ct)
    {
        var createdNpcs = new List<NpcRecord>();
        foreach (var enclave in config.Enclaves)
        {
            foreach (var team in enclave.Teams)
            {
                for (var i = 0; i < team.Npcs.Number; i++)
                {
                    var branch = team.Npcs.Configuration.Branch ?? MilitaryUnits.GetServiceBranch();
                    var npc = NpcRecord.TransformToNpc(Npc.Generate(branch));
                    npc.Team = team.Name;
                    npc.Campaign = config.Campaign;
                    npc.Enclave = enclave.Name;
                    npc.Id = npc.NpcProfile.Id;
                    createdNpcs.Add(npc);
                }
            }
        }
            
        foreach (var npc in createdNpcs)
        {
            foreach (var job in npc.NpcProfile.Employment.EmploymentRecords)
            {
                //get same company departments and highest ranked in that department

                var managerList = createdNpcs.Where(x => x.Id != npc.Id 
                                                         && x.NpcProfile.Employment.EmploymentRecords.Any(
                                                             o => o.Company == job.Company 
                                                                  && o.Department == job.Department 
                                                                  && o.Level >= job.Level)).ToList();

                if (managerList.Any())
                {
                    var manager = managerList.RandomElement();
                    job.Manager = manager.Id;
                }
            }
        }

        this._context.Npcs.AddRange(createdNpcs);
        this._context.SaveChanges();
        return createdNpcs;
    }

    /// <summary>
    /// Export insider threat specific csv file
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getInsiderThreatCsv")]
    [HttpGet("csv")]
    public async Task<IActionResult> GetAsCsv()
    {
        var engine = new FileHelperEngine<NPCToInsiderThreatCsv>();
        engine.HeaderText = engine.GetFileHeader();

        var list = await this._context.Npcs.ToListAsync();
        var finalList = NPCToInsiderThreatCsv.ConvertToCsv(list.ToList());
            
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, finalList);
        await streamWriter.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream, "text/csv", $"insider_threat_{Guid.NewGuid()}.csv");
    }
}