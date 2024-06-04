// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace ghosts.api.Controllers;

[Route("view-social")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewSocialController : Controller
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;

    public ViewSocialController()
    {
        this._configuration = Program.ApplicationSettings;
    }

    [HttpGet]
    public IActionResult Index()
    {
        throw new NotImplementedException();
        // ViewBag.IsEnabled = this._configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled;
        // if (!this._configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled)
        // {
        //     return View();
        // }
        //
        // var path = SocialGraphJob.GetSocialGraphFile();
        // if (!System.IO.File.Exists(path))
        // {
        //     ViewBag.IsEnabled = false;
        //     return View();
        // }
        //
        // var graphs = JsonConvert.DeserializeObject<List<NpcSocialGraph>>(System.IO.File.ReadAllText(path));
        // _log.Info($"SocialGraph loaded from disk...");
        //
        // return View(graphs);
    }

    [HttpGet("{id}")]
    public IActionResult Detail(Guid id)
    {
        throw new NotImplementedException();
        // ViewBag.IsEnabled = this._configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled;
        // if (!this._configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled)
        // {
        //     return View();
        // }
        //
        // var path = SocialGraphJob.GetSocialGraphFile();
        // if (!System.IO.File.Exists(path))
        // {
        //     ViewBag.IsEnabled = false;
        //     return View();
        // }
        //
        // var graph = JsonConvert.DeserializeObject<List<NpcSocialGraph>>(System.IO.File.ReadAllText(path)).FirstOrDefault(x => x.Id == id);
        // _log.Info($"SocialGraph loaded from disk...");
        //
        // return View(graph);
    }

    [HttpGet("{id}/interactions")]
    public IActionResult Interactions(string id)
    {
        ViewBag.Id = id;
        return View();
    }

    [HttpGet("{id}/file")]
    public IActionResult File(Guid id)
    {
        throw new NotImplementedException();
        // var path = SocialGraphJob.GetSocialGraphFile();
        // var graph = JsonConvert.DeserializeObject<List<NpcSocialGraph>>(System.IO.File.ReadAllText(path)).FirstOrDefault(x => x.Id == id);
        // _log.Info("SocialGraph loaded from disk...");
        //
        // var interactions = new InteractionMap();
        // var startTime = DateTime.Now.AddMinutes(-graph.Connections.Count).AddMinutes(-1);
        // var endTime = DateTime.Now.AddMinutes(1);
        //
        // var node = new Node
        // {
        //     id = id.ToString(),
        //     start = startTime,
        //     end = endTime
        // };
        // interactions.nodes.Add(node);
        //
        // foreach (var connection in graph.Connections)
        // {
        //     if (connection.Interactions.Count < 1)
        //         continue;
        //     node = new Node
        //     {
        //         id = connection.Id.ToString(),
        //         start = startTime.AddMinutes(connection.Interactions.Min(x=>x.Step)),
        //         end = endTime
        //     };
        //     interactions.nodes.Add(node);
        // }
        //
        // foreach (var learning in graph.Knowledge)
        // {
        //     var link = new Link
        //     {
        //         start = startTime.AddMinutes(learning.Step),
        //         source = learning.To.ToString(),
        //         target = learning.From.ToString()
        //     };
        //     link.end = link.start.AddMinutes(1);
        //     interactions.links.Add(link);
        // }
        //
        // // var content = System.IO.File.ReadAllText(
        // //     "/Users/dustin/Projects/ghosts-animator/src/ghosts-animator-api/wwwroot/view-social/files/5c0e56b44362ec8e2621299d2ddce5ac68e4e1b11e08ac4547075b0e6374d9083a589eec442479ef7876be75215b8499cf9463743191cfe01e4ca3cb826135e5.json");
        // var content = JsonConvert.SerializeObject(interactions);
        // var fileBytes = Encoding.ASCII.GetBytes(content);
        // return File(fileBytes, "application/json", $"{Guid.NewGuid()}.json");
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
            this.nodes = new List<Node>();
            this.links = new List<Link>();
        }
    }
}