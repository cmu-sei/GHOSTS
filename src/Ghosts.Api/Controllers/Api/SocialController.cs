// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[Produces("application/json")]
[Route("api/[controller]")]
public class SocialController(ApplicationDbContext context) : Controller
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration = Program.ApplicationSettings;

    [ProducesResponseType(typeof(IReadOnlyList<NpcRecord>), 200)]
    [SwaggerOperation("SocialGraphsGet")]
    [HttpGet]
    public async Task<IReadOnlyList<NpcRecord>> Index()
    {
        if (!IsSocialGraphEnabled())
        {
            return new List<NpcRecord>();
        }

        var npcs = await LoadNpcsAsync();
        if (npcs == null)
        {
            return new List<NpcRecord>();
        }

        _log.Info("NPCs with social data loaded from database.");
        return npcs;
    }

    [ProducesResponseType(typeof(NpcRecord), 200)]
    [SwaggerOperation("SocialGraphsGetById")]
    [HttpGet("{id}")]
    public async Task<NpcRecord> Detail(Guid id)
    {
        if (!IsSocialGraphEnabled())
        {
            return new NpcRecord();
        }

        var npc = await LoadNpcByIdAsync(id);
        if (npc == null)
        {
            return new NpcRecord();
        }

        _log.Info("NPC with social data loaded from database.");
        return npc;
    }

    [ProducesResponseType(typeof(FileContentResult), 200)]
    [SwaggerOperation("SocialGraphsGetFile")]
    [HttpGet("{id}/file")]
    public async Task<IActionResult> File(Guid id)
    {
        var npc = await LoadNpcByIdAsync(id);
        if (npc == null)
        {
            return null;
        }

        _log.Info("NPC social data loaded from database.");
        var interactions = CreateInteractionMap(npc);

        var content = JsonConvert.SerializeObject(interactions); // Serialize the interaction map to JSON
        var fileBytes = Encoding.ASCII.GetBytes(content); // Convert JSON to bytes

        return File(fileBytes, "application/json", $"{Guid.NewGuid()}.json"); // Return as a JSON file
    }

    private bool IsSocialGraphEnabled()
    {
        return _configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled;
    }

    private async Task<List<NpcRecord>> LoadNpcsAsync()
    {
        var npcs = await context.Npcs
            .Include(n => n.Connections)
                .ThenInclude(c => c.Interactions)
            .Include(n => n.Knowledge)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .ToListAsync();
        return npcs;
    }

    private async Task<NpcRecord> LoadNpcByIdAsync(Guid id)
    {
        var npcs = await LoadNpcsAsync();
        return npcs?.FirstOrDefault(x => x.Id == id);
    }

    private static InteractionMap CreateInteractionMap(NpcRecord npc)
    {
        var interactions = new InteractionMap();
        var startTime = DateTime.Now.AddMinutes(-npc.Connections.Count).AddMinutes(-1); // Adjust start time
        var endTime = DateTime.Now.AddMinutes(1); // End time

        // Create a node for the main NPC
        interactions.Nodes.Add(new Node { Id = npc.Id.ToString(), Start = startTime, End = endTime });

        // Add nodes for each connection
        foreach (var connection in npc.Connections ?? Enumerable.Empty<NpcSocialConnection>())
        {
            if (connection.Interactions == null || connection.Interactions.Count < 1) continue;

            interactions.Nodes.Add(new Node
            {
                Id = string.IsNullOrWhiteSpace(connection.Id)
                    ? connection.ConnectedNpcId.ToString()
                    : connection.Id,
                Start = startTime.AddMinutes(connection.Interactions.Min(x => x.Step)),
                End = endTime
            });
        }

        // Add links for each knowledge entry
        foreach (var learning in npc.Knowledge ?? Enumerable.Empty<NpcLearning>())
        {
            interactions.Links.Add(new Link
            {
                Start = startTime.AddMinutes(learning.Step),
                Source = learning.ToNpcId.ToString(),
                Target = learning.FromNpcId.ToString(),
                End = startTime.AddMinutes(1) // Adjusting end time for links
            });
        }

        return interactions;
    }
}
