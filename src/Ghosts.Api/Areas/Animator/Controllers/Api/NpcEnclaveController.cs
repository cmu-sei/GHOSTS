// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FileHelpers;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Areas.Animator.Controllers.Api;

/// <summary>
/// Export or delete all NPCs from a specific enclave
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/[controller]/{campaign}/{enclave}")]
public class NpcEnclaveController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NpcEnclaveController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all NPCs by from a specific enclave that is part of a specific campaign
    /// </summary>
    /// <param name="enclave">The name of the enclave</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerOperation("getEnclave")]
    [HttpGet]
    public IEnumerable<NpcRecord> GetEnclave(string campaign, string enclave)
    {
        return _context.Npcs.Where(x => x.Campaign == campaign && x.Enclave == enclave).ToList();
    }

    /// <summary>
    /// Gets the csv output of a query
    /// </summary>
    /// <param name="enclave">The name of the enclave</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getEnclaveCsv")]
    [HttpGet("csv")]
    public IActionResult GetAsCsv(string campaign, string enclave)
    {
        var engine = new FileHelperEngine<NPCToCsv>();
        var list = GetEnclave(campaign, enclave);

        var filteredList = list.Select(n => new NPCToCsv {Id = n.Id, Email = n.NpcProfile.Email}).ToList();

        var stream = new MemoryStream();
        TextWriter streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, filteredList);
        streamWriter.Flush();
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream, "text/csv", $"{Guid.NewGuid()}.csv");
    }

    /// <summary>
    /// Delete All NPCs in a specific enclave
    /// </summary>
    /// <param name="enclave"></param>
    /// <param name="campaign"></param>
    /// <returns></returns>
    [ProducesResponseType((int) HttpStatusCode.OK)]
    [SwaggerResponse((int) HttpStatusCode.OK)]
    [SwaggerOperation("deleteEnclave")]
    [HttpDelete]
    public async Task DeleteEnclave(string campaign, string enclave)
    {
        var list = GetEnclave(campaign, enclave);
        this._context.Npcs.RemoveRange(list);
        await this._context.SaveChangesAsync();
    }
        
    /// <summary>
    /// Get a CSV file containing all of the requested properties of NPCs in an enclave
    /// </summary>
    /// <param name="campaign"></param>
    /// <param name="enclave"></param>
    /// <param name="fieldsToReturn"></param>
    /// <returns></returns>
    [HttpPost("custom")]
    public IActionResult GetReducedNpcs(string campaign, string enclave, [FromBody] string[] fieldsToReturn)
    {
        var npcList = GetEnclave(campaign, enclave);
        var npcDetails = new Dictionary<string, Dictionary<string, string>>();
            
        foreach (var npc in npcList) {
            var npcProperties = new NPCReduced(fieldsToReturn, npc).PropertySelection;
            var name = npc.NpcProfile.Name;
            var npcName = name.ToString();
            if (npcName != null) npcDetails[npcName] = npcProperties;
        }
            
        var enclaveCsv = new EnclaveReducedCsv(fieldsToReturn, npcDetails);
        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        streamWriter.Write(enclaveCsv.CsvData);
        streamWriter.Flush();
        stream.Seek(0, SeekOrigin.Begin);
        return File(stream, "text/csv", $"{Guid.NewGuid()}.csv");
    }
}