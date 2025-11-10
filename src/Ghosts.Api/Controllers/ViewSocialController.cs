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

namespace Ghosts.Api.Controllers;

[Controller]
[Route("view-social")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewSocialController(ApplicationDbContext context) : Controller
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration = Program.ApplicationSettings;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsSocialGraphEnabled())
        {
            return View(); // Social graph is not enabled, return default view
        }

        var graphs = await LoadSocialGraphsAsync();
        if (graphs == null)
        {
            return View(); // Return default view if no graphs found
        }

        _log.Info("SocialGraph loaded from disk.");
        return View(graphs); // Return the view with the graph data
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        if (!IsSocialGraphEnabled())
        {
            return View(); // Social graph not enabled
        }

        var graph = await LoadGraphByIdAsync(id);
        if (graph == null)
        {
            return NotFound(); // Graph with the given ID was not found
        }

        _log.Info("SocialGraph loaded from disk.");
        return View(graph); // Return view with the graph data
    }

    [HttpGet("{id}/interactions")]
    public IActionResult Interactions(string id)
    {
        ViewBag.Id = id;
        return View();
    }

    [HttpGet("{id}/file")]
    public async Task<IActionResult> File(Guid id)
    {
        var graph = await LoadGraphByIdAsync(id);
        if (graph == null)
        {
            return NotFound(); // Graph not found
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
        var graphs = await context.NpcSocialGraphs
            .Include(sg => sg.Connections)
                .ThenInclude(c => c.Interactions)
            .Include(sg => sg.Knowledge)
            .Include(sg => sg.Beliefs)
            .Include(sg => sg.Preferences)
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
