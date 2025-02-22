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
using ghosts.api.Areas.Animator.Infrastructure.Models;
using ghosts.api.Infrastructure.Models;
using ghosts.api.Infrastructure.Services;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using ghosts.api.Infrastructure.Extensions;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Controllers.Api;

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcsController(ApplicationDbContext context, INpcService service) : ControllerBase
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Returns all generated NPCs in the system (caution, could return a very large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcProfile&gt;</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcRecord>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcRecord>>))]
    [SwaggerOperation("NpcsGetAll")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> NpcsGetAll()
    {
        return Ok(await service.GetAll());
    }

    /// <summary>
    /// Returns all generated NPCs in the system (caution, could return a very large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcProfile&gt;</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcRecord>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcRecord>>))]
    [SwaggerOperation("NpcsCreate")]
    [HttpPost]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> NpcsCreate(NpcProfile npc)
    {
        return Ok(await service.CreateOne(npc));
    }

    /// <summary>
    /// Returns name and Id for all NPCs in the system (caution, could return a large amount of data)
    /// </summary>
    /// <returns>IEnumerable&lt;NpcNameId&gt;</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcNameId>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcNameId>>))]
    [SwaggerOperation("NpcsGetList")]
    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<NpcNameId>>> NpcsGetList()
    {
        return Ok(await service.GetListAsync());
    }

    /// <summary>
    /// Get NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(ActionResult<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcRecord>))]
    [SwaggerOperation("NpcsGetById")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NpcRecord>> NpcsGetById(Guid id)
    {
        return Ok(await service.GetById(id));
    }

    /// <summary>
    /// Delete NPC by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK)]
    [SwaggerOperation("NpcsDeleteById")]
    [HttpDelete("{id:guid}")]
    public async Task NpcsDeleteById(Guid id)
    {
        await service.DeleteById(id);
    }

    /// <summary>
    /// Get NPC photo by specific Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("NpcsGetAvatarById")]
    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> NpcsGetAvatarById(Guid id)
    {
        // Get NPC and find image
        var npc = await service.GetById(id);
        if (npc == null) return NotFound();

        // Determine the image path
        var imagePath = string.IsNullOrEmpty(npc.NpcProfile.PhotoLink)
            ? ApplicationDetails.ConfigurationFiles.DefaultNpcImage
            : npc.NpcProfile.PhotoLink;

        // Check if the image file exists
        if (!System.IO.File.Exists(imagePath))
        {
            _log.Warn($"Npc {id} — File does not exist! {imagePath} — switching to default npc image");
            imagePath = ApplicationDetails.ConfigurationFiles.DefaultNpcImage;
        }

        // is path within the app? is it a jpg?
        if (!imagePath.IsPathWithinAppScope(ApplicationDetails.InstalledPath))
        {
            _log.Warn($"Npc {id} — Bad image path! {imagePath} — switching to default npc image");
            imagePath = ApplicationDetails.ConfigurationFiles.DefaultNpcImage;
        }

        // is an image?
        if (!new HashSet<string> { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(imagePath)?.ToLower()))
        {
            _log.Warn($"Npc {id} — Not an image! {imagePath} — switching to default npc image");
            imagePath = ApplicationDetails.ConfigurationFiles.DefaultNpcImage;
        }

        // Load image as stream and return as file result
        var stream = new FileStream(imagePath!, FileMode.Open, FileAccess.Read);
        return File(stream, "image/jpg", $"{npc.NpcProfile.Name.ToString()!.Replace(" ", "_")}.jpg");
    }

    /// <summary>
    /// Get a subset of details about a specific NPC
    /// </summary>
    /// <param name="npcId"></param>
    /// <param name="fieldsToReturn"></param>
    /// <returns></returns>
    [HttpPost("npc/{npcId:guid}")]
    [Obsolete("Obsolete")]
    [SwaggerOperation("NpcsGetReducedFields")]
    public async Task<object> NpcsGetReducedFields(Guid npcId, [FromBody] string[] fieldsToReturn)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var npc = await service.GetById(npcId);
        if (npc == null) return null;
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
    [SwaggerOperation("NpcsEnclaveGet")]
    [HttpGet("{campaign}/{enclave}")]
    public async Task<IEnumerable<NpcRecord>> NpcsEnclaveGet(string campaign, string enclave)
    {
        return await service.GetEnclave(campaign, enclave);
    }

    /// <summary>
    /// Gets the csv output of a query
    /// </summary>
    /// <param name="enclave">The name of the enclave</param>
    /// <param name="campaign">The name of the campaign the enclave is part of</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("NpcsEnclaveGetCsv")]
    [HttpGet("{campaign}/{enclave}/csv")]
    [Obsolete("Obsolete")]
    public async Task<IActionResult> NpcsEnclaveGetCsv(string campaign, string enclave)
    {
        var engine = new FileHelperEngine<NPCToCsv>();
        var list = await NpcsEnclaveGet(campaign, enclave);

        var filteredList = list.Select(n => new NPCToCsv { Id = n.Id, Email = n.NpcProfile.Email }).ToList();

        var stream = new MemoryStream();
        TextWriter streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, filteredList);
        await streamWriter.FlushAsync();
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
    [SwaggerOperation("NpcsEnclaveDelete")]
    [HttpDelete("{campaign}/{enclave}")]
    public async Task NpcsEnclaveDelete(string campaign, string enclave)
    {
        var list = await NpcsEnclaveGet(campaign, enclave);
        context.Npcs.RemoveRange(list);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Get a CSV file containing all of the requested properties of NPCs in an enclave
    /// </summary>
    /// <param name="campaign"></param>
    /// <param name="enclave"></param>
    /// <param name="fieldsToReturn"></param>
    /// <returns></returns>
    [HttpPost("{campaign}/{enclave}/custom")]
    [Obsolete("Obsolete")]
    [SwaggerOperation("NpcsEnclaveReducedFields")]
    public async Task<IActionResult> NpcsEnclaveReducedFields(string campaign, string enclave, [FromBody] string[] fieldsToReturn)
    {
        var npcList = await NpcsEnclaveGet(campaign, enclave);
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
        await streamWriter.WriteAsync(enclaveCsv.CsvData);
        await streamWriter.FlushAsync();
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
    [SwaggerOperation("NpcsTeamGet")]
    [HttpGet("{campaign}/{enclave}/{team}")]
    public async Task<IEnumerable<NpcRecord>> NpcsTeamGet(string campaign, string enclave, string team)
    {
        return await service.GetTeam(campaign, enclave, team);
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
    [SwaggerOperation("NpcsTeamGetCsv")]
    [Obsolete("Obsolete")]
    [HttpGet("{campaign}/{enclave}/{team}/csv")]
    public async Task<IActionResult> NpcsTeamGetCsv(string campaign, string enclave, string team)
    {
        var engine = new FileHelperEngine<NPCToCsv>();
        var list = await NpcsTeamGet(team, enclave, campaign);

        var filteredList = list.Select(n => new NPCToCsv { Id = n.Id, Email = n.NpcProfile.Email }).ToList();

        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, filteredList);
        await streamWriter.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream, "text/csv", $"{Guid.NewGuid()}.csv");
    }

    /// <summary>
    /// Gets the tfvars output of a team of NPCs
    /// </summary>
    /// <param name="configuration"></param>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerResponse((int)HttpStatusCode.BadRequest, Type = typeof(string))]
    [Obsolete("Obsolete")]
    [SwaggerOperation("NpcsTfVarsGet")]
    [HttpPost("tfvars")]
    public async Task<IActionResult> NpcsTfVarsGet(TfVarsConfiguration configuration)
    {
        if (!ModelState.IsValid || configuration == null || !configuration.IsValid())
        {
            return BadRequest(ModelState);
        }

        var s = new StringBuilder("users = {").Append(Environment.NewLine);
        var list = await NpcsTeamGet(configuration.Campaign, configuration.Enclave, configuration.Team);

        var pool = configuration.GetIpPool();
        foreach (var item in pool)
            if (context.NpcIps.Any(o => o.IpAddress == item && o.Enclave == configuration.Enclave))
                pool.Remove(item);

        if (pool.Count < list.Count())
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
            context.NpcIps.Add(new NPCIpAddress { IpAddress = ip, NpcId = npc.Id, Enclave = npc.Enclave });
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

        context.SaveChanges();

        return File(Encoding.UTF8.GetBytes
                (s.ToString()), "text/tfvars", $"{Guid.NewGuid()}.tfvars");
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
    [SwaggerOperation("NpcsInsiderThreatCreate")]
    [HttpPost("insiderThreat")]
    [Obsolete("Obsolete")]
    [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
    [SwaggerResponse((int)HttpStatusCode.BadRequest, Type = typeof(string))]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> NpcsInsiderThreatCreate(InsiderThreatGenerationConfiguration config, CancellationToken ct)
    {
        if (!ModelState.IsValid || config?.Enclaves == null)
        {
            return BadRequest(ModelState);
        }

        var createdNpcs = new List<NpcRecord>();
        foreach (var enclave in config.Enclaves)
        {
            if (enclave.Teams == null) continue;
            foreach (var team in enclave.Teams)
            {
                if (team.Npcs == null) continue;
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

                if (managerList.Count != 0)
                {
                    var manager = managerList.RandomElement();
                    job.Manager = manager.Id;
                }
            }
        }

        context.Npcs.AddRange(createdNpcs);
        await context.SaveChangesAsync(ct);
        return createdNpcs;
    }

    /// <summary>
    /// Export insider threat specific csv file
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("NpcsInsiderThreatGetCsv")]
    [HttpGet("insiderThreat/csv")]
    [Obsolete("Obsolete")]
    public async Task<IActionResult> NpcsInsiderThreatGetCsv()
    {
        var engine = new FileHelperEngine<NPCToInsiderThreatCsv>();
        engine.HeaderText = engine.GetFileHeader();

        var list = await context.Npcs.ToListAsync();
        var finalList = NPCToInsiderThreatCsv.ConvertToCsv(list.ToList());

        var stream = new MemoryStream();
        var streamWriter = new StreamWriter(stream);
        engine.WriteStream(streamWriter, finalList);
        await streamWriter.FlushAsync();
        stream.Seek(0, SeekOrigin.Begin);

        return File(stream, "text/csv", $"insider_threat_{Guid.NewGuid()}.csv");
    }
}
