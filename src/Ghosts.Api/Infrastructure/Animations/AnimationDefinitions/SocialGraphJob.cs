// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Animator.Extensions;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Weighted_Randomizer;

namespace Ghosts.Api.Infrastructure.Animations.AnimationDefinitions;

public class SocialGraphJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ActivityHub> _hub;
    private readonly string[] _knowledgeTopics;
    private readonly CancellationToken _token;
    private readonly ThreadLocal<Random> _random = new(() => new Random());
    private const string SavePath = "_output/socialgraph/";

    public SocialGraphJob(ApplicationSettings config, IServiceScopeFactory scopeFactory, IHubContext<ActivityHub> hub, CancellationToken token)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _token = token;
        _knowledgeTopics = File.ReadAllLines("config/knowledge_topics.txt");
    }

    public void Start(int agentCount)
    {
        _ = Task.Run(() => RunAsync(agentCount), _token);
    }

    public async Task RunAsync(int agentCount)
    {
        using var initScope = _scopeFactory.CreateScope();
        var context = initScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var agents = context.Npcs.RandPick(agentCount).ToList();

        _log.Info("Running social graph steps...");

        while (!_token.IsCancellationRequested)
        {
            var tasks = agents.Select(agent => ProcessAgentAsync(agent.Id)).ToArray();
            await Task.WhenAll(tasks);
            await Task.Delay(_config.AnimatorSettings.Animations.SocialGraph.TurnLength, _token);
        }
    }

    private IEnumerable<NpcSocialGraph.SocialConnection> GetSocialConnectionFromNpc(NpcRecord npc)
    {
        var connection = new NpcSocialGraph.SocialConnection();
        connection.Id = npc.Id;
        connection.Name = npc.NpcProfile.Name.ToString();
        connection.RelationshipStatus = 0;
        connection.Distance = "";
        connection.Interactions = new List<NpcSocialGraph.Interaction>();
        var connections = new List<NpcSocialGraph.SocialConnection>();
        connections.Add(connection);
        return connections;
    }


    private async Task ProcessAgentAsync(Guid npcId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var npc = context.Npcs.FirstOrDefault(n => n.Id == npcId);
            if (npc == null) return;

            var graph = npc.NpcSocialGraph ?? InitializeGraph(npc, context);
            if (graph.CurrentStep > _config.AnimatorSettings.Animations.SocialGraph.MaximumSteps)
                return;

            graph.CurrentStep++;
            var interactCount = _random.Value.NextDouble().GetNumberByDecreasingWeights(0, graph.Connections.Count, 0.4);
            var targets = graph.Connections.RandPick(interactCount);

            // if no one knows anyone, It's hard to get started, so "meet someone at the coffee counter"
            if(!targets.Any())
                targets = GetSocialConnectionFromNpc(context.Npcs.RandPick(1).FirstOrDefault());

            foreach (var target in targets)
            {
                var topic = TryLearn(graph, context);
                if (!string.IsNullOrEmpty(topic))
                {
                    var learning = new NpcSocialGraph.Learning(graph.Id, target.Id, topic, graph.CurrentStep, 1);
                    graph.Knowledge.Add(learning);
                    await _hub.Clients.All.SendAsync("show", graph.CurrentStep, target.Id, "knowledge",
                        $"learned more about {topic} (1)", DateTime.Now.ToString(CultureInfo.InvariantCulture), _token);

                    if (learning.From == npc.Id) continue; // can't improve the relationship with oneself (philosophically true?
                    var connection = graph.Connections.FirstOrDefault(c => c.Id == learning.From);
                    if (connection != null)
                    {
                        connection.RelationshipStatus++;
                        await Task.Delay(1500, _token);
                        await _hub.Clients.All.SendAsync("show", graph.CurrentStep, target.Id, "relationship",
                            $"{npc.NpcProfile.Name} improved relationship with {target.Name}",
                            DateTime.Now.ToString(CultureInfo.InvariantCulture), _token);
                    }
                    else
                    {
                        var newConnection = context.Npcs.FirstOrDefault(c => c.Id == learning.From);
                        if (newConnection != null)
                        {
                            graph.Connections.Add(new NpcSocialGraph.SocialConnection()
                            {
                                Id = newConnection.Id, Name = newConnection.NpcProfile.Name.ToString()
                            });
                            await Task.Delay(1500, _token);
                            await _hub.Clients.All.SendAsync("show", graph.CurrentStep, target.Id, "relationship",
                                $"{npc.NpcProfile.Name} improved relationship with {target.Name}",
                                DateTime.Now.ToString(CultureInfo.InvariantCulture), _token);
                        }
                    }
                }
            }

            npc.NpcSocialGraph = graph;
            context.Update(npc);
            await context.SaveChangesAsync(_token);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error processing NPC {npcId}");
        }
    }

    private NpcSocialGraph InitializeGraph(NpcRecord npc, ApplicationDbContext context)
    {
        var connections = context.Npcs.OrderBy(n => n.Enclave).ThenBy(n => n.Team).Take(10).ToList();
        var graph = new NpcSocialGraph
        {
            Id = npc.Id,
            Name = npc.NpcProfile.Name.ToString(),
            Connections = connections.Select(c => new NpcSocialGraph.SocialConnection
            {
                Id = c.Id,
                Name = c.NpcProfile.Name.ToString()
            }).ToList()
        };

        npc.NpcSocialGraph = graph;
        context.Update(npc);
        context.SaveChanges();
        return graph;
    }

    private string TryLearn(NpcSocialGraph graph, ApplicationDbContext context)
    {
        var npc = context.Npcs.FirstOrDefault(n => n.Id == graph.Id);
        if (npc == null) return string.Empty;

        var chance = _config.AnimatorSettings.Animations.SocialGraph.ChanceOfKnowledgeTransfer;
        chance += npc.NpcProfile.Education.Degrees.Count * 0.1;
        chance += npc.NpcProfile.MentalHealth.OverallPerformance * 0.1;
        chance += graph.Knowledge.Count * 0.1;

        if (!chance.ChanceOfThisValue()) return string.Empty;

        var randomizer = new DynamicWeightedRandomizer<string>();
        foreach (var k in _knowledgeTopics)
        {
            if (!randomizer.Contains(k))
                randomizer.Add(k, graph.Knowledge.Count(x => x.Topic == k) + 1);
        }

        return randomizer.NextWithReplacement();
    }
}
