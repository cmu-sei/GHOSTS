// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileHelpers;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Areas.Animator.Controllers.Api;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NpcsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all generated NPCs in the system (caution, could return a large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcProfile&gt;</returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerOperation("getNPCs")]
    [HttpGet]
    public IEnumerable<NpcRecord> Get()
    {
        return this._context.Npcs.ToList();
    }

    /// <summary>
    /// Returns name and Id for all NPCs in the system (caution, could return a large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcNameId&gt;</returns>
    [ProducesResponseType(typeof(IEnumerable<NpcNameId>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IEnumerable<NpcNameId>))]
    [SwaggerOperation("getNPCList")]
    [HttpGet("list")]
    public IEnumerable<NpcNameId> List()
    {
        return this._context.Npcs.Select(item => new NpcNameId()
            { Id = item.Id, Name = item.NpcProfile.Name.First + " " + item.NpcProfile.Name.Last }).ToList();
    }

    /// <summary>
    /// Get NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(NpcRecord), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(NpcRecord))]
    [SwaggerOperation("getNPCById")]
    [HttpGet("{id:guid}")]
    public NpcRecord GetById(Guid id)
    {
        return this._context.Npcs.FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    /// Delete NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerOperation("deleteNPCById")]
    [HttpDelete("{id:guid}")]
    public async Task DeleteById(Guid id)
    {
        var o = await this._context.Npcs.FindAsync(id);
        this._context.Npcs.Remove(o);
        await this._context.SaveChangesAsync();
    }

    /// <summary>
    /// Get NPC photo by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getNpcAvatarById")]
    [HttpGet("{id:guid}/photo")]
    public IActionResult GetPhotoById(Guid id)
    {
        //get npc and find image
        var npc = this._context.Npcs.FirstOrDefault(x => x.Id == id);
        if (npc == null) return NotFound();
        //load image as stream
        var stream = new FileStream(npc.NpcProfile.PhotoLink, FileMode.Open);
        return File(stream, "image/jpg", $"{npc.NpcProfile.Name.ToString().Replace(" ", "_")}.jpg");
    }

    /// <summary>
    /// Create one NPC (handy for syncing up from ghosts core api)
    /// </summary>
    /// <returns>NPC Profile</returns>
    [ProducesResponseType(typeof(NpcRecord), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(NpcRecord))]
    [SwaggerOperation("createNpc")]
    [HttpPost]
    public async Task<NpcRecord> Create(NpcProfile npcProfile, bool generate)
    {
        NpcRecord npc;
        if (generate)
        {
            npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
            npc.NpcProfile.Name = npcProfile.Name;
            npc.NpcProfile.Email = npcProfile.Email;
        }
        else
        {
            npc = NpcRecord.TransformToNpc(npcProfile);
        }

        npc.NpcProfile.Id = Guid.NewGuid();
        npc.NpcProfile.Created = DateTime.UtcNow;
        npc.Id = npc.NpcProfile.Id;

        this._context.Npcs.Add(npc);
        await this._context.SaveChangesAsync();
        return npc;
    }

    /// <summary>
    /// Get a subset of details about a specific NPC
    /// </summary>
    /// <param name="npcId"></param>
    /// <param name="fieldsToReturn"></param>
    /// <returns></returns>
    [HttpPost("npc/{npcId:guid}")]
    public object GetNpcReduced(Guid npcId, [FromBody] string[] fieldsToReturn)
    {
        var npc = this._context.Npcs.FirstOrDefault(x => x.Id == npcId);
        return new NPCReduced(fieldsToReturn, npc).PropertySelection;
    }

    /// <summary>
    /// Gets all NPCs by from a specific enclave that is part of a specific campaign
    /// </summary>
    /// <param name="enclave">The name of the enclave</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerOperation("getEnclave")]
    [HttpGet("{campaign}/{enclave}")]
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
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getEnclaveCsv")]
    [HttpGet("{campaign}/{enclave}/csv")]
    public IActionResult GetAsCsv(string campaign, string enclave)
    {
        var engine = new FileHelperEngine<NPCToCsv>();
        var list = GetEnclave(campaign, enclave);

        var filteredList = list.Select(n => new NPCToCsv { Id = n.Id, Email = n.NpcProfile.Email }).ToList();

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
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerOperation("deleteEnclave")]
    [HttpDelete("{campaign}/{enclave}")]
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
    [HttpPost("{campaign}/{enclave}/custom")]
    public IActionResult GetReducedNpcs(string campaign, string enclave, [FromBody] string[] fieldsToReturn)
    {
        var npcList = GetEnclave(campaign, enclave);
        var npcDetails = new Dictionary<string, Dictionary<string, string>>();

        foreach (var npc in npcList)
        {
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

    /// <summary>
    /// Gets all NPCs by from a specific team in a specific enclave that is part of a specific campaign
    /// </summary>
    /// <param name="team">The name of the team</param>
    /// <param name="enclave">The name of the enclave the team is in</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerOperation("getTeam")]
    [HttpGet("{campaign}/{enclave}/{team}")]
    public IEnumerable<NpcRecord> GetTeam(string campaign, string enclave, string team)
    {
        return this._context.Npcs.Where(x => x.Campaign == campaign && x.Enclave == enclave && x.Team == team).ToList();
    }

    /// <summary>
    /// Gets the csv output of a query
    /// </summary>
    /// <param name="team">The name of the team</param>
    /// <param name="enclave">The name of the enclave the team is in</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getTeamCsv")]
    [HttpGet("{campaign}/{enclave}/{team}/csv")]
    public IActionResult GetAsCsv(string campaign, string enclave, string team)
    {
        var engine = new FileHelperEngine<NPCToCsv>();
        var list = this.GetTeam(team, enclave, campaign).ToList();

        var filteredList = list.Select(n => new NPCToCsv { Id = n.Id, Email = n.NpcProfile.Email }).ToList();

        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, filteredList);
        streamWriter.Flush();
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream, "text/csv", $"{Guid.NewGuid()}.csv");
    }

    /// <summary>
    /// Gets the tfvars output of a team of NPCs
    /// </summary>
    /// <param name="configuration"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getBuildTfVars")]
    [HttpPost("tfvars")]
    public IActionResult GetTeamAsTfVars(TfVarsConfiguration configuration)
    {
        var s = new StringBuilder("users = {").Append(Environment.NewLine);
        var list = this.GetTeam(configuration.Campaign, configuration.Enclave, configuration.Team).ToList();

        var pool = configuration.GetIpPool();
        foreach (var item in pool)
            if (this._context.NpcIps.Any(o => o.IpAddress == item && o.Enclave == configuration.Enclave))
                pool.Remove(item);

        if (pool.Count < list.Count)
        {
            throw new ArgumentException("There are not enough unused ip addresses for the number of NPCs selected");
        }

        var i = 0;
        foreach (var npc in list)
        {
            var n = string.Empty;
            foreach (var c in npc.NpcProfile.Finances.CreditCards)
            {
                n = c.Number;
                break;
            }

            var ip = pool.RandomElement();
            this._context.NpcIps.Add(new NPCIpAddress { IpAddress = ip, NpcId = npc.Id, Enclave = npc.Enclave });
            pool.Remove(ip);

            s.Append("\tuser-").Append(i).Append(" = {").Append(Environment.NewLine);
            s.Append("\t\tipaddr = ").Append(ip).Append(Environment.NewLine);
            s.Append("\t\tmask = ").Append(configuration.Mask).Append(Environment.NewLine);
            s.Append("\t\tgateway = ").Append(configuration.Gateway).Append(Environment.NewLine);
            s.Append("\t\ttitle = ").Append(npc.NpcProfile.Rank.Abbr).Append(Environment.NewLine);
            s.Append("\t\tfirst = ").Append(npc.NpcProfile.Name.First).Append(Environment.NewLine);
            s.Append("\t\tlast = ").Append(npc.NpcProfile.Name.Last).Append(Environment.NewLine);
            s.Append("\t\taddress = ").Append(npc.NpcProfile.Address[0].Address1).Append(Environment.NewLine);
            s.Append("\t\tcity = ").Append(npc.NpcProfile.Address[0].City).Append(Environment.NewLine);
            s.Append("\t\tstate = ").Append(npc.NpcProfile.Address[0].State).Append(Environment.NewLine);
            s.Append("\t\tzip = ").Append(npc.NpcProfile.Address[0].PostalCode).Append(Environment.NewLine);
            s.Append("\t\temail = ").Append(npc.NpcProfile.Email).Append(Environment.NewLine);
            s.Append("\t\tpassword = ").Append(npc.NpcProfile.Password).Append(Environment.NewLine);
            s.Append("\t\tcreditcard = ").Append(n).Append(Environment.NewLine);
            s.Append("\t}").Append(Environment.NewLine);
            s.Append(Environment.NewLine);
            i++;
        }

        this._context.SaveChanges();

        return File(Encoding.UTF8.GetBytes
                (s.ToString()), "text/tfvars", $"{Guid.NewGuid()}.tfvars");
    }

    /// <summary>
    /// Delete All NPCs in a specific team
    /// </summary>
    /// <param name="team"></param>
    /// <param name="enclave"></param>
    /// <param name="campaign"></param>
    /// <returns></returns>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerOperation("deleteTeam")]
    [HttpDelete("{campaign}/{enclave}/{team}")]
    public void DeleteTeam(string campaign, string enclave, string team)
    {
        throw new NotImplementedException();

        // var list = this.GetTeam(campaign, enclave, team).ToList();
        // foreach (var ipFilter in list.Select(npc => npc.Id).Select(npcId => Builders<NPCIpAddress>.Filter.And(
        //              Builders<NPCIpAddress>.Filter.Eq("NpcId", npcId),
        //              Builders<NPCIpAddress>.Filter.Eq("Enclave", enclave))))
        // {
        //     _mongoIps.DeleteMany(ipFilter);
        // }
        //
        // var npcFilter = BuildTeamFilter(campaign, enclave, team);
        // _mongo.DeleteMany(npcFilter);
    }

    /// <summary>
    /// Create an insider threat specific NPC build
    /// </summary>
    /// <param name="config"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IEnumerable<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IEnumerable<NpcRecord>))]
    [SwaggerRequestExample(typeof(InsiderThreatGenerationConfiguration),
        typeof(InsiderThreatGenerationConfigurationExample))]
    [SwaggerOperation("createInsiderThreatBuild")]
    [HttpPost("insiderThreat")]
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
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("getInsiderThreatCsv")]
    [HttpGet("insiderThreat/csv")]
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