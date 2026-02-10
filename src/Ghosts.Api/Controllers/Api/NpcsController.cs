// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Domain.Code;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

public class CreatePreferenceRequest
{
    public Guid ToNpcId { get; set; }
    public Guid FromNpcId { get; set; }
    public string Name { get; set; }
    public long Step { get; set; }
    public decimal Weight { get; set; }
    public decimal Strength { get; set; }
}

public class CreateConnectionRequest
{
    public Guid ConnectedNpcId { get; set; }
    public string Name { get; set; }
    public string Distance { get; set; }
    public int RelationshipStatus { get; set; }
}

[ApiController]
[Produces("application/json")]
[Route("api/[controller]")]
public class NpcsController(
    ApplicationDbContext context,
    INpcService service,
    IHubContext<ActivityHub> activityHubContext,
    IMachineUpdateService machineUpdateService) : ControllerBase
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
    /// Creates an NPC
    /// </summary>
    /// <returns>NpcProfile</returns>
    [ProducesResponseType(typeof(ActionResult<NpcRecord>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcRecord>))]
    [SwaggerOperation("NpcsCreate")]
    [HttpPost]
    public async Task<ActionResult<NpcRecord>> NpcsCreate(NpcProfile npc)
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
    /// Get all activity records for a specific NPC
    /// </summary>
    /// <param name="id">The NPC ID</param>
    /// <returns>IEnumerable&lt;NpcActivity&gt;</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcActivity>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcActivity>>))]
    [SwaggerOperation("NpcsGetActivityById")]
    [HttpGet("{id:guid}/activity")]
    public async Task<ActionResult<IEnumerable<NpcActivity>>> NpcGetActivity(Guid id)
    {
        return Ok(await service.GetActivity(id));
    }

    /// <summary>
    /// Create a new activity record for a specific NPC
    /// </summary>
    /// <param name="id">The NPC ID</param>
    /// <param name="activityType">The type of activity</param>
    /// <param name="detail">Activity details</param>
    /// <returns>NpcActivity</returns>
    [ProducesResponseType(typeof(ActionResult<NpcActivity>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcActivity>))]
    [SwaggerOperation("NpcsCreateActivityById")]
    [HttpPost("{id:guid}/activity")]
    public async Task<ActionResult<NpcActivity>> NpcCreateActivity(Guid id, string activityType, string detail)
    {
        return Ok(await service.CreateActivity(id, activityType, detail));
    }

    /// <summary>
    /// Get all preferences for a specific NPC
    /// </summary>
    /// <param name="id">The NPC ID</param>
    /// <returns>IEnumerable&lt;NpcPreference&gt;</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcPreference>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcPreference>>))]
    [SwaggerOperation("NpcsGetPreferencesById")]
    [HttpGet("{id:guid}/preferences")]
    public async Task<ActionResult<IEnumerable<NpcPreference>>> NpcGetPreferences(Guid id)
    {
        return Ok(await service.GetPreferences(id));
    }

    [ProducesResponseType(typeof(ActionResult<NpcPreference>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcPreference>))]
    [SwaggerOperation("NpcsCreatePreferenceById")]
    [HttpPost("{id:guid}/preferences")]
    public async Task<ActionResult<NpcPreference>> NpcCreatePreference(Guid id, [FromBody] CreatePreferenceRequest request)
    {
        return Ok(await service.CreatePreference(id, request.ToNpcId, request.FromNpcId,
            request.Name, request.Step, request.Weight, request.Strength));
    }

    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcSocialConnection>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcSocialConnection>>))]
    [SwaggerOperation("NpcsGetConnectionsById")]
    [HttpGet("{id:guid}/connections")]
    public async Task<ActionResult<IEnumerable<NpcSocialConnection>>> NpcGetConnections(Guid id)
    {
        return Ok(await service.GetConnections(id));
    }

    [ProducesResponseType(typeof(ActionResult<NpcSocialConnection>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<NpcSocialConnection>))]
    [SwaggerOperation("NpcsCreateConnectionById")]
    [HttpPost("{id:guid}/connections")]
    public async Task<ActionResult<NpcSocialConnection>> NpcCreateConnection(Guid id, [FromBody] CreateConnectionRequest request)
    {
        return Ok(await service.CreateConnection(id, request.ConnectedNpcId,
            request.Name, request.Distance, request.RelationshipStatus));
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
    /// Send AI-driven instructions to the NPCs
    /// </summary>
    /// <returns></returns>
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(IActionResult))]
    [SwaggerOperation("NpcsAiCommand")]
    [HttpPost("command")]
    public async Task<IActionResult> Command([FromBody] AiModels.ActionRequest actionRequest, CancellationToken ct)
    {
        IEnumerable<NpcRecord> npcs = null;
        Random random = new Random();
        var scale = actionRequest.Scale;
        if (scale > 1)
        {
            scale *= random.Next(1, 10);
            npcs = (context.Npcs.ToList().Shuffle(random).Take(scale)).ToList();
        }
        else
        {
            try
            {
                npcs = context.Npcs.Where(x => x.NpcProfile.Name.First.ToLower() == actionRequest.Who.ToLower());
            }
            catch
            {
                //pass
            }

            if (npcs == null || !npcs.Any())
            {
                try
                {
                    npcs = context.Npcs
                        .Where(x => x.NpcProfile != null &&
                            (x.NpcProfile.Name.First + " " + x.NpcProfile.Name.Last).ToLower().StartsWith(actionRequest.Who.ToLower()));
                }
                catch
                {
                    //pass
                }
            }

            if (npcs == null || !npcs.Any())
            {
                npcs = context.Npcs.ToList().Shuffle(random).Take(1);
            }
        }

        foreach (var npc in npcs)
        {
            //actionRequest = await GetSentiment(actionRequest);

            // clean up outliers
            if (actionRequest.Handler.ToLower().Contains("word") || actionRequest.Handler.ToLower().Contains("docs"))
                actionRequest.Handler = "Word";
            if (actionRequest.Handler.ToLower().Contains("browser"))
                actionRequest.Handler = random.NextDouble() < 0.8 ? "BrowserFirefox" : "BrowserChrome";
            if (actionRequest.Handler.ToLower().Contains("email"))
                actionRequest.Handler = "Outlook";

            // now map
            switch (actionRequest.Handler.ToLower())
            {
                case "aws":
                case "azure":
                case "blog":
                case "blogdrupal":
                case "browserchrome":
                case "browsercrawl":
                case "browserfirefox":
                case "clicks":
                case "cmd":
                case "excel":
                case "executefile":
                case "ftp":
                case "notepad":
                case "outlook":
                case "pidgin":
                case "powerpoint":
                case "powershell":
                case "print":
                case "rdp":
                case "reboot":
                case "sftp":
                case "sharepoint":
                case "social":
                case "ssh":
                case "watcher":
                case "wmi":
                case "word":
                    await ProcessHandledAction(npc, actionRequest, ct);
                    break;
                default:
                    await ProcessUnhandledAction(npc, actionRequest, ct);
                    break;
            }
        }

        return NoContent();
    }

    private async Task ProcessUnhandledAction(NpcRecord npc, AiModels.ActionRequest actionRequest, CancellationToken ct)
    {
        actionRequest.Sentiment ??= "neutral";
        await activityHubContext.Clients.All.SendAsync("show",
            "1",
            npc.Id,
            "activity-other",
            actionRequest,
            DateTime.Now.ToString(CultureInfo.InvariantCulture),
            cancellationToken: ct);
    }

    private async Task ProcessHandledAction(NpcRecord npc, AiModels.ActionRequest actionRequest, CancellationToken ct)
    {
        // create and send timeline to machine that npc is associated with
        var machineUpdate = await machineUpdateService.CreateByActionRequest(npc, actionRequest, ct);

        if (machineUpdate != null)
        {
            actionRequest.Sentiment ??= "neutral";
            await activityHubContext.Clients.All.SendAsync("show",
                "1",
                npc.Id,
                "activity",
                actionRequest,
                DateTime.Now.ToString(CultureInfo.InvariantCulture),
                cancellationToken: ct);
        }
    }
}
