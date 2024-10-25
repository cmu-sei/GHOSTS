// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Controllers
{
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
            interactions.nodes.Add(new Node
            {
                id = graph.Id.ToString(),
                start = startTime,
                end = endTime
            });

            // Add nodes for each connection
            foreach (var connection in graph.Connections)
            {
                if (connection.Interactions.Count < 1) continue;

                interactions.nodes.Add(new Node
                {
                    id = connection.Id.ToString(),
                    start = startTime.AddMinutes(connection.Interactions.Min(x => x.Step)),
                    end = endTime
                });
            }

            // Add links for each knowledge entry
            foreach (var learning in graph.Knowledge)
            {
                interactions.links.Add(new Link
                {
                    start = startTime.AddMinutes(learning.Step),
                    source = learning.To.ToString(),
                    target = learning.From.ToString(),
                    end = startTime.AddMinutes(1) // Adjusting end time for links
                });
            }

            return interactions;
        }

        public class Link
        {
            public string source { get; set; }
            public string target { get; set; }
            public DateTime start { get; set; }
            public DateTime end { get; set; }
        }

        public class Node
        {
            public string id { get; set; }
            public DateTime start { get; set; }
            public DateTime end { get; set; }
        }

        public class InteractionMap
        {
            public List<Node> nodes { get; set; }
            public List<Link> links { get; set; }

            public InteractionMap()
            {
                nodes = new List<Node>();
                links = new List<Link>();
            }
        }
    }
}
