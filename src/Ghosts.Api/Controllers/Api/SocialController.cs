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

    [ProducesResponseType(typeof(IReadOnlyList<NpcSocialGraph>), 200)]
    [SwaggerOperation("SocialGraphsGet")]
    [HttpGet]
    public async Task<IReadOnlyList<NpcSocialGraph>> Index()
    {
        if (!IsSocialGraphEnabled())
        {
            return new List<NpcSocialGraph>();
        }

        var graphs = await LoadSocialGraphsAsync();
        if (graphs == null)
        {
            return new List<NpcSocialGraph>();
        }

        _log.Info("SocialGraph loaded from disk.");
        return graphs;
    }

    [ProducesResponseType(typeof(NpcSocialGraph), 200)]
    [SwaggerOperation("SocialGraphsGetById")]
    [HttpGet("{id}")]
    public async Task<NpcSocialGraph> Detail(Guid id)
    {
        if (!IsSocialGraphEnabled())
        {
            return new NpcSocialGraph();
        }

        var graph = await LoadGraphByIdAsync(id);
        if (graph == null)
        {
            return new NpcSocialGraph();
        }

        _log.Info("SocialGraph loaded from disk.");
        return graph;
    }

    [ProducesResponseType(typeof(FileContentResult), 200)]
    [SwaggerOperation("SocialGraphsGetFile")]
    [HttpGet("{id}/file")]
    public async Task<IActionResult> File(Guid id)
    {
        var graph = await LoadGraphByIdAsync(id);
        if (graph == null)
        {
            return null;
        }

        _log.Info("SocialGraph loaded from disk.");
        var interactions = CreateInteractionMap(graph);

        var content = JsonConvert.SerializeObject(interactions); // Serialize the interaction map to JSON
        var fileBytes = Encoding.ASCII.GetBytes(content); // Convert JSON to bytes

        return File(fileBytes, "application/json", $"{Guid.NewGuid()}.json"); // Return as a JSON file
    }

    private bool IsSocialGraphEnabled()
    {
        return _configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled;
    }

    private async Task<List<NpcSocialGraph>> LoadSocialGraphsAsync()
    {
        var graphs = await context.Npcs
            .Where(x => x.NpcSocialGraph != null)
            .Select(x => x.NpcSocialGraph)
            .ToListAsync();
        return graphs;
    }

    private async Task<NpcSocialGraph> LoadGraphByIdAsync(Guid id)
    {
        var graphs = await LoadSocialGraphsAsync();
        return graphs?.FirstOrDefault(x => x.Id == id);
    }

    private static InteractionMap CreateInteractionMap(NpcSocialGraph graph)
    {
        var interactions = new InteractionMap();
        var startTime = DateTime.Now.AddMinutes(-graph.Connections.Count).AddMinutes(-1); // Adjust start time
        var endTime = DateTime.Now.AddMinutes(1); // End time

        // Create a node for the main graph
        interactions.Nodes.Add(new Node { Id = graph.Id.ToString(), Start = startTime, End = endTime });

        // Add nodes for each connection
        foreach (var connection in graph.Connections ?? Enumerable.Empty<NpcSocialConnection>())
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
        foreach (var learning in graph.Knowledge ?? Enumerable.Empty<NpcLearning>())
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
