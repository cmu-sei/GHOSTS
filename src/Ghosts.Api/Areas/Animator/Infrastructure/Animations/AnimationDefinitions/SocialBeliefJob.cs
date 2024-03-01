// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Ghosts.Animator.Extensions;
using ghosts.api.Areas.Animator.Hubs;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.Animations.AnimationDefinitions;

public class SocialBeliefJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly ApplicationDbContext _context;
    private List<SocialGraph> _socialGraphs;
    private readonly Random _random;
    private bool _isEnabled = true;
    private CancellationToken _cancellationToken;

    private const string SavePath = "_output/socialbelief/";
    private const string SocialGraphFile = "social_belief.json";
    private readonly IHubContext<ActivityHub> _activityHubContext;

    public static string GetSocialGraphFile()
    {
        return SavePath + SocialGraphFile;
    }

    public static string[] Beliefs =
    {
        "Strong passwords are essential.", "Regular software updates matter.", "Public Wi-Fi is risky.",
        "Antivirus software is a must.",
        "Encryption protects data.", "Phishing attacks are common.", "Two-factor authentication helps.",
        "Social engineering is a threat.",
        "IoT devices can be vulnerable.", "Cyberwarfare affects nations.", "Ransomware can cripple businesses.",
        "Insider threats are real.",
        "Dark web is a breeding ground.", "DDoS attacks disrupt services.", "Hacktivists promote causes."
    };

    public SocialBeliefJob(ApplicationSettings configuration, ApplicationDbContext context, Random random,
        IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        try
        {
            this._activityHubContext = activityHubContext;
            this._configuration = configuration;
            this._random = random;
            this._context = context;
            this._cancellationToken = cancellationToken;

            this.LoadSocialBeliefs();

            if (this._socialGraphs.Count > 0 &&
                this._socialGraphs[0].CurrentStep > _configuration.AnimatorSettings.Animations.SocialGraph.MaximumSteps)
            {
                _log.Trace("SocialBelief has exceed maximum steps. Sleeping...");
                return;
            }

            _log.Info("SocialBelief loaded, running steps...");
            while (this._isEnabled && !this._cancellationToken.IsCancellationRequested)
            {
                foreach (var graph in this._socialGraphs)
                {
                    this.Step(graph);
                }

                // post-step activities: saving results and reporting on them
                File.WriteAllText(GetSocialGraphFile(),
                    JsonConvert.SerializeObject(this._socialGraphs, Formatting.None));
                this.Report();
                _log.Info($"Step complete, sleeping for {this._configuration.AnimatorSettings.Animations.SocialGraph.TurnLength}ms");
                Thread.Sleep(this._configuration.AnimatorSettings.Animations.SocialBelief.TurnLength);
            }
        }
        catch (ThreadInterruptedException)
        {
            // continue
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private void LoadSocialBeliefs()
    {
        var graphs = new List<SocialGraph>();
        var path = SavePath;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        path += SocialGraphFile;
        if (File.Exists(path))
        {
            graphs = JsonConvert.DeserializeObject<List<SocialGraph>>(File.ReadAllText(path));
            _log.Info("SocialBelief loaded from disk...");
        }
        else
        {
            var list = this._context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o => o.Team).Take(10).ToList();
            foreach (var item in list)
            {
                //need to build a list of connections for every npc
                var graph = new SocialGraph
                {
                    Id = item.Id,
                    Name = item.NpcProfile.Name.ToString()
                };
                foreach (var connection in from sub in list
                         where sub.Id != item.Id
                         select new SocialGraph.SocialConnection
                         {
                             Id = sub.Id,
                             Name = sub.NpcProfile.Name.ToString()
                         })
                {
                    graph.Connections.Add(connection);
                }

                graphs.Add(graph);
            }

            _log.Info("SocialBelief created from DB...");
        }

        this._socialGraphs = graphs;
    }

    private void Step(SocialGraph graph)
    {
        if (graph.CurrentStep > this._configuration.AnimatorSettings.Animations.SocialBelief.MaximumSteps)
        {
            _log.Trace($"Maximum steps met: {graph.CurrentStep - 1}. SocialBelief is exiting...");
            this._isEnabled = false;
            return;
        }

        graph.CurrentStep++;

        SocialGraph.Belief belief = null;

        if (graph.Beliefs != null)
        {
            belief = graph.Beliefs.MaxBy(x => x.Step);
        }
        else
        {
            graph.Beliefs = new List<SocialGraph.Belief>();
        }

        if (belief == null)
        {
            var l = Convert.ToDecimal(this._random.NextDouble() * (0.75 - 0.25) + 0.25);
            belief = new SocialGraph.Belief(graph.Id, graph.Id, Beliefs.RandomFromStringArray(), graph.CurrentStep, l,
                (decimal)0.5);
        }

        var bayes = new Bayes(graph.CurrentStep, belief.Likelihood, belief.Posterior, 1 - belief.Likelihood,
            1 - belief.Posterior);
        var newBelief = new SocialGraph.Belief(graph.Id, graph.Id, Beliefs.RandomFromStringArray(), graph.CurrentStep,
            belief.Likelihood, bayes.PosteriorH1);
        graph.Beliefs.Add(newBelief);

        //post to hub
        this._activityHubContext.Clients.All.SendAsync("show",
            newBelief.Step,
            newBelief.To.ToString(),
            "belief",
            $"{graph.Name} has deeper belief in {newBelief.Name}",
            DateTime.Now.ToString(CultureInfo.InvariantCulture)
        );
    }

    private void Report()
    {
        var line = new StringBuilder();

        //write header
        line.Append(SocialGraph.Belief.ToHeader())
            .Append(Environment.NewLine);

        //now write each person
        foreach (var npc in this._socialGraphs)
        {
            line.Append(npc.Id).Append(',').Append(npc.Name).Append(",H_1,,,")
                .Append(Environment.NewLine);

            foreach (var belief in npc.Beliefs)
            {
                line.Append(",,,").Append(belief.Step).Append(',').Append(belief.Likelihood).Append(',')
                    .Append(belief.Posterior)
                    .Append(Environment.NewLine);
            }
        }

        _log.Trace(line.ToString().TrimEnd(','));

        File.WriteAllText($"{SavePath}social_beliefs.csv", line.ToString().TrimEnd(',') + Environment.NewLine);
    }
}