// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Ghosts.Api.Areas.Animator.Infrastructure;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Animator.Extensions;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Ghosts.Api.Infrastructure.Animations.AnimationDefinitions;

public class SocialBeliefJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly ApplicationDbContext _context;
    private readonly Random _random;
    private bool _isEnabled = true;
    private readonly CancellationToken _cancellationToken;

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
            _activityHubContext = activityHubContext;
            _configuration = configuration;
            _random = random;

            using var innerScope = scopeFactory.CreateScope();
            _context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            _cancellationToken = cancellationToken;

            var npcs = _context.Npcs.RandPick(10).ToList();

            _log.Info("SocialBelief loaded, running steps...");
            while (_isEnabled && !_cancellationToken.IsCancellationRequested)
            {
                foreach (var npc in npcs)
                {
                    Step(npc);
                }

                _log.Info($"Step complete, sleeping for {_configuration.AnimatorSettings.Animations.SocialBelief.TurnLength}ms");
                Thread.Sleep(_configuration.AnimatorSettings.Animations.SocialBelief.TurnLength);
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
        // Reload the NPC with navigation properties
        var npcWithData = _context.Npcs
            .Include(n => n.Connections)
            .Include(n => n.Beliefs)
            .Include(n => n.Preferences)
            .FirstOrDefault(n => n.Id == npc.Id);

        if (npcWithData == null) return;

        // Initialize connections if needed
        if (npcWithData.Connections == null || !npcWithData.Connections.Any())
        {
            //need to build a list of connections for every npc
            npcWithData.Connections = new List<NpcSocialConnection>();

            var connections = _context.Npcs
                .OrderBy(o => o.Enclave)
                .ThenBy(o => o.Team)
                .Take(10)
                .ToList();

            foreach (var connection in connections)
            {
                npcWithData.Connections.Add(new NpcSocialConnection
                {
                    Id = $"{npcWithData.Id}:{connection.Id}",
                    ConnectedNpcId = connection.Id,
                    Name = connection.NpcProfile.Name.ToString(),
                    NpcId = npcWithData.Id
                });
            }

            var o = _context.SaveChanges();
            Console.WriteLine($"{o} rows were affected.");

            _log.Trace($"Social connections saved for {npcWithData.NpcProfile.Name}...");
        }

        if (npcWithData.CurrentStep > _configuration.AnimatorSettings.Animations.SocialBelief.MaximumSteps)
        {
            _log.Trace($"Maximum steps met: {npcWithData.CurrentStep - 1}. SocialBelief is exiting...");
            _isEnabled = false;
            return;
        }

        npcWithData.CurrentStep++;

        NpcBelief belief = null;

        if (npcWithData.Beliefs != null)
        {
            belief = npcWithData.Beliefs.MaxBy(x => x.Step);
        }
        else
        {
            npcWithData.Beliefs = new List<NpcBelief>();
        }

        if (belief == null)
        {
            var l = Convert.ToDecimal(_random.NextDouble() * (0.75 - 0.25) + 0.25);
            belief = new NpcBelief(0, npcWithData.Id, npcWithData.Id, npcWithData.Id, Beliefs.RandomFromStringArray(), npcWithData.CurrentStep, l,
                (decimal)0.5);
        }

        var bayes = new Bayes(npcWithData.CurrentStep, belief.Likelihood, belief.Posterior, 1 - belief.Likelihood,
            1 - belief.Posterior);
        var newBelief = new NpcBelief(0, npcWithData.Id, npcWithData.Id, npcWithData.Id, Beliefs.RandomFromStringArray(), npcWithData.CurrentStep,
            belief.Likelihood, bayes.PosteriorH1);
        npcWithData.Beliefs.Add(newBelief);

        //post to hub
        _activityHubContext.Clients.All.SendAsync("show",
            newBelief.Step,
            newBelief.ToNpcId.ToString(),
            "belief",
            $"{npcWithData.NpcProfile.Name} has updated posterior of {Math.Round(newBelief.Posterior, 2)} in {newBelief.Name}",
            DateTime.Now.ToString(CultureInfo.InvariantCulture), cancellationToken: _cancellationToken);

        // EF Core will track changes automatically - just save
        var affectedRows = _context.SaveChanges();
        Console.WriteLine($"{affectedRows} rows were affected.");

    }
}
