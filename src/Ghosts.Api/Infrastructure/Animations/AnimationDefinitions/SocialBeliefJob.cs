// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Ghosts.Animator.Extensions;
using ghosts.api.Areas.Animator.Hubs;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.Animations.AnimationDefinitions;

public class SocialBeliefJob 
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly ApplicationDbContext _context;
    private readonly Random _random;
    private bool _isEnabled = true;
    private CancellationToken _cancellationToken;

    private readonly IHubContext<ActivityHub> _activityHubContext;

    public static string[] Beliefs =
    {
        // "Strong passwords are essential.", "Regular software updates matter.", "Public Wi-Fi is risky.",
        // "Antivirus software is a must.",
        // "Encryption protects data.", "Phishing attacks are common.", "Two-factor authentication helps.",
        // "Social engineering is a threat.",
        // "IoT devices can be vulnerable.", "Cyberwarfare affects nations.", "Ransomware can cripple businesses.",
        // "Insider threats are real.",
        // "Dark web is a breeding ground.", "DDoS attacks disrupt services.", "Hacktivists promote causes."
        "I should vote for candidate A", "I should vote for candidate B"
    };

    public SocialBeliefJob(ApplicationSettings configuration, IServiceScopeFactory scopeFactory, Random random,
        IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        try
        {
            this._activityHubContext = activityHubContext;
            this._configuration = configuration;
            this._random = random;
            
            using var innerScope = scopeFactory.CreateScope();
            this._context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            this._cancellationToken = cancellationToken;

            // if (this._socialGraphs.Count > 0 &&
            //     this._socialGraphs[0].CurrentStep > _configuration.AnimatorSettings.Animations.SocialGraph.MaximumSteps)
            // {
            //     _log.Trace("SocialBelief has exceed maximum steps. Sleeping...");
            //     return;
            // }
            
            var npcs = this._context.Npcs.RandPick(10).ToList();

            _log.Info("SocialBelief loaded, running steps...");
            while (this._isEnabled && !this._cancellationToken.IsCancellationRequested)
            {
                foreach (var npc in npcs)
                {
                    this.Step(npc);
                }

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

    private void Step(NpcRecord npc)
    {
        var graph = npc.NpcSocialGraph;
        if (graph == null)
        {
            //need to build a list of connections for every npc
            graph = new NpcSocialGraph
            {
                Id = npc.Id,
                Name = npc.NpcProfile.Name.ToString()
            };

            var connections = this._context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o => o.Team).Take(10).ToList();
            foreach (var connection in connections)
            {
                graph.Connections.Add(new NpcSocialGraph.SocialConnection
                {
                    Id = connection.NpcProfile.Id,
                    Name = connection.NpcProfile.Name.ToString()
                });
            }

            npc.NpcSocialGraph = graph;
            this._context.Npcs.Update(npc); // Explicitly set the state to Modified
            var o = this._context.SaveChanges();
            Console.WriteLine($"{o} rows were affected.");

            _log.Trace($"Social graph saved for {npc.NpcProfile.Name}...");
        }
        
        if (graph.CurrentStep > this._configuration.AnimatorSettings.Animations.SocialBelief.MaximumSteps)
        {
            _log.Trace($"Maximum steps met: {graph.CurrentStep - 1}. SocialBelief is exiting...");
            this._isEnabled = false;
            return;
        }

        graph.CurrentStep++;

        NpcSocialGraph.Belief belief = null;

        if (graph.Beliefs != null)
        {
            belief = graph.Beliefs.MaxBy(x => x.Step);
        }
        else
        {
            graph.Beliefs = new List<NpcSocialGraph.Belief>();
        }

        if (belief == null)
        {
            var l = Convert.ToDecimal(this._random.NextDouble() * (0.75 - 0.25) + 0.25);
            belief = new NpcSocialGraph.Belief(graph.Id, graph.Id, Beliefs.RandomFromStringArray(), graph.CurrentStep, l,
                (decimal)0.5);
        }

        var bayes = new Bayes(graph.CurrentStep, belief.Likelihood, belief.Posterior, 1 - belief.Likelihood,
            1 - belief.Posterior);
        var newBelief = new NpcSocialGraph.Belief(graph.Id, graph.Id, Beliefs.RandomFromStringArray(), graph.CurrentStep,
            belief.Likelihood, bayes.PosteriorH1);
        graph.Beliefs.Add(newBelief);

        //post to hub
        this._activityHubContext.Clients.All.SendAsync("show",
            newBelief.Step,
            newBelief.To.ToString(),
            "belief",
            $"{graph.Name} has updated posterior of {Math.Round(newBelief.Posterior, 2)} in {newBelief.Name}",
            DateTime.Now.ToString(CultureInfo.InvariantCulture), cancellationToken: _cancellationToken);

        this._context.Npcs.Update(npc); // Explicitly set the state to Modified
        var affectedRows = this._context.SaveChanges();
        Console.WriteLine($"{affectedRows} rows were affected.");

    }
}