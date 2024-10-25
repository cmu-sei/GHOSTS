// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Hubs;
using ghosts.api.Infrastructure.Models;
using Ghosts.Animator.Extensions;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Weighted_Randomizer;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions;

public class SocialGraphJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApplicationDbContext _context;
    private readonly List<NpcSocialGraph> _socialGraphs;
    private readonly Random _random;
    private const string SavePath = "_output/socialgraph/";
    private readonly string[] _knowledgeArray;
    private bool _isEnabled = true;
    private readonly CancellationToken _cancellationToken;
    private readonly IHubContext<ActivityHub> _activityHubContext;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public SocialGraphJob(ApplicationSettings configuration, IServiceScopeFactory scopeFactory, Random random,
        IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        try
        {
            _activityHubContext = activityHubContext;
            _configuration = configuration;
            _random = random;
            _scopeFactory = scopeFactory;
            _cancellationToken = cancellationToken;
            _socialGraphs = new List<NpcSocialGraph>();
            _knowledgeArray = GetAllKnowledge();

            // if (this._socialGraphs.Count > 0 &&
            //     this._socialGraphs[0].CurrentStep > _configuration.AnimatorSettings.Animations.SocialGraph.MaximumSteps)
            // {
            //     _log.Trace("Graph has exceed maximum steps. Sleeping...");
            //     return;
            // }

            using var innerScope = _scopeFactory.CreateScope();
            _context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var npcs = _context.Npcs.RandPick(10).ToList();

            _log.Info("SocialGraph loaded, running steps...");
            while (_isEnabled && !_cancellationToken.IsCancellationRequested)
            {
                foreach (var npc in npcs)
                {
                    _ = Step(npc);
                }

                _log.Info(
                    $"Step complete, sleeping for {_configuration.AnimatorSettings.Animations.SocialGraph.TurnLength}ms");
                Thread.Sleep(_configuration.AnimatorSettings.Animations.SocialGraph.TurnLength);
            }
        }
        catch (ThreadInterruptedException e)
        {
            // continue
            _log.Error(e);
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    // (a) agents “know” some number of other agents, affecting their interactions, and this “know” (relationshipStatus) value changes over time
    // (b) a file -- social_graph.json — gets pushed down to each agent and represents “other agents in relation to this agent”
    //      it is stored in ./instance, containing id, name, and “know” status of the other agents in the enclave
    // (c) each step, n interactions may take place, picking from social_graph.json and interacting
    // (d) This interaction may affect knowledge (the existing preferences key/value pair —
    //      the interaction could create new knowledge, or increase an existing one by some value
    private async Task Step(NpcRecord npc)
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

            var connections = _context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o => o.Team).Take(10).ToList();
            foreach (var connection in connections)
            {
                graph.Connections.Add(new NpcSocialGraph.SocialConnection
                {
                    Id = connection.NpcProfile.Id,
                    Name = connection.NpcProfile.Name.ToString()
                });
            }

            await _semaphore.WaitAsync(_cancellationToken);

            try
            {
                npc.NpcSocialGraph = graph;
                _context.Entry(npc).State = EntityState.Modified;
                await _context.SaveChangesAsync(_cancellationToken);
                _log.Trace($"Social graph saved for {npc.NpcProfile.Name}...");
            }
            catch (Exception e)
            {
                _log.Trace($"Error creating new social graph: {e}. Continuing...");
            }
            finally
            {
                _semaphore.Release();
            }
        }
        else
        {
            if (npc.NpcSocialGraph.Id != npc.Id)
            {
                // reports of 00000... ids in the social graph. not sure how this is happening? old version of api that is out there?
                await _semaphore.WaitAsync(_cancellationToken);

                try
                {
                    npc.NpcSocialGraph.Id = npc.Id;
                    _context.Entry(npc).State = EntityState.Modified;
                    await _context.SaveChangesAsync(_cancellationToken);
                    _log.Trace($"Social graph id corrected for {npc.NpcProfile.Id}...");
                }
                catch (Exception e)
                {
                    _log.Trace($"Error correcting social graph id: {e}. Continuing...");
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        _log.Trace("Social graph step proceeding...");

        if (graph.CurrentStep > _configuration.AnimatorSettings.Animations.SocialGraph.MaximumSteps)
        {
            _log.Trace($"Maximum steps met: {graph.CurrentStep - 1}. Social graph is exiting...");
            _isEnabled = false;
            return;
        }

        graph.CurrentStep++;
        _log.Trace($"{graph.CurrentStep}: {graph.Id} is interacting...");

        //get number of agents to interact with, weighted by decreasing weights calculation and who is in our graph
        var numberOfAgentsToInteract =
            _random.NextDouble().GetNumberByDecreasingWeights(0, graph.Connections.Count, 0.4);

        //pick other agent(s), allowing multiple interactions with the same person because that seems likely
        //TODO: make this weighted by distance and/or influenced by like knowledge
        var agentsToInteract = graph.Connections.RandPick(numberOfAgentsToInteract);

        //now interact
        foreach (var agent in agentsToInteract)
        {
            var interaction = new NpcSocialGraph.Interaction
            {
                Step = graph.CurrentStep,
                Value = 1
            };
            var interactingWith = graph.Connections.FirstOrDefault(x => x.Id == agent.Id);
            interactingWith?.Interactions.Add(interaction);

            //knowledge transferred?
            var topic = CalculateLearning(graph);
            if (topic is not null)
            {
                var learning = new NpcSocialGraph.Learning(graph.Id, agent.Id, topic, graph.CurrentStep, 1);
                graph.Knowledge.Add(learning);

                //post to hub
                await _activityHubContext.Clients.All.SendAsync("show",
                    graph.CurrentStep,
                    agent.Id,
                    "knowledge",
                    $"learned more about {learning.Topic} ({learning.Value})",
                    DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    new CancellationToken()
                );

                Thread.Sleep(_configuration.AnimatorSettings.Animations.SocialGraph.TurnLength);

                _log.Trace(learning.ToString);

                //does relationship value change?
                if (CalculateRelationshipChange(graph, learning))
                {
                    var connection = graph.Connections.FirstOrDefault(x => x.Id == learning.From);
                    if (connection is not null)
                    {
                        connection.RelationshipStatus++;

                        var npcTo = _context.Npcs.Include(x => x.NpcProfile.Name)
                            .FirstOrDefault(x => x.Id == learning.To);
                        var npcFrom = _context.Npcs.Include(x => x.NpcProfile.Name)
                            .FirstOrDefault(x => x.Id == learning.From);

                        var o =
                            $"{graph.CurrentStep}: {npcFrom.NpcProfile.Name}'s relationship improved with {npcTo.NpcProfile.Name}...";
                        _log.Trace(o);

                        //post to hub
                        await _activityHubContext.Clients.All.SendAsync("show",
                            graph.CurrentStep,
                            agent.Id,
                            "relationship",
                            o,
                            DateTime.Now.ToString(CultureInfo.InvariantCulture)
                        );
                    }
                }
            }
            else
            {
                var o = $"{graph.CurrentStep}: {graph.Id} didn't learn...";
                _log.Trace(o);

                //post to hub
                await _activityHubContext.Clients.All.SendAsync("show",
                    graph.CurrentStep,
                    agent.Id,
                    "relationship",
                    o,
                    DateTime.Now.ToString(CultureInfo.InvariantCulture),
                    new CancellationToken()
                );
            }
        }

        //decay
        //https://pubsonline.informs.org/doi/10.1287/orsc.1090.0468
        //in knowledge - the world changes, and so there is some amount of knowledge that is now obsolete
        //in relationships - without work, relationships can decay
        var stepsToDecay = _configuration.AnimatorSettings.Animations.SocialGraph.Decay.StepsTo;
        if (graph.CurrentStep > stepsToDecay)
        {
            foreach (var k in graph.Knowledge.ToList().DistinctBy(x => x.Topic))
            {
                if (graph.Knowledge.Where(x => x.Topic == k.Topic).Sum(x => x.Value) > stepsToDecay)
                {
                    if (_configuration.AnimatorSettings.Animations.SocialGraph.Decay.ChanceOf.ChanceOfThisValue())
                    {
                        if (graph.Knowledge.Where(x => x.Topic == k.Topic).MaxBy(x => x.Step)?.Step <
                            graph.CurrentStep - stepsToDecay)
                        {
                            var learning =
                                new NpcSocialGraph.Learning(graph.Id, graph.Id, k.Topic, graph.CurrentStep, -1);
                            graph.Knowledge.Add(learning);

                            //post to hub
                            await _activityHubContext.Clients.All.SendAsync("show",
                                graph.CurrentStep,
                                graph.Id,
                                "knowledge",
                                $"had knowledge decay in {k.Topic} occur",
                                DateTime.Now.ToString(CultureInfo.InvariantCulture),
                                new CancellationToken()
                            );

                            break;
                        }
                    }
                }
            }

            foreach (var c in graph.Connections)
            {
                if (c.Interactions.Count > 1 &&
                    c.Interactions.MaxBy(x => x.Step)?.Step < graph.CurrentStep - stepsToDecay)
                {
                    var interaction = new NpcSocialGraph.Interaction
                    {
                        Step = graph.CurrentStep,
                        Value = -1
                    };
                    c.Interactions.Add(interaction);

                    //post to hub
                    await _activityHubContext.Clients.All.SendAsync("show",
                        graph.CurrentStep,
                        graph.Id,
                        "relationship",
                        $"Experienced general relationship decay",
                        DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        new CancellationToken()
                    );
                }
            }
        }

        _socialGraphs.Add(graph);
    }

    private string CalculateLearning(NpcSocialGraph graph)
    {
        string knowledge = null;

        var chance = _configuration.AnimatorSettings.Animations.SocialGraph.ChanceOfKnowledgeTransfer;
        var npc = _context.Npcs.FirstOrDefault(x => x.Id == graph.Id);
        if (npc == null)
            return string.Empty;

        chance += npc.NpcProfile.Education.Degrees.Count * .1;
        chance += npc.NpcProfile.MentalHealth.OverallPerformance * .1;
        chance += graph.Knowledge.Count * .1;

        if (chance.ChanceOfThisValue())
        {
            //knowledge is probably weighted to what you already know
            var randomizer = new DynamicWeightedRandomizer<string>();
            foreach (var k in _knowledgeArray)
            {
                if (!randomizer.Contains(k))
                    randomizer.Add(k, graph.Knowledge.Count(x => x.Topic == k) + 1);
            }

            knowledge = randomizer.NextWithReplacement();
        }

        return knowledge;
    }

    private static bool CalculateRelationshipChange(NpcSocialGraph graph, NpcSocialGraph.Learning learning)
    {
        return graph.Knowledge.Count(x => x.From == learning.From) > 1;
    }

    private static string[] GetAllKnowledge()
    {
        var k = File.ReadAllText("config/knowledge_topics.txt").Split(Environment.NewLine);
        Array.Sort(k);
        return k;
    }

    private void ReportLearning()
    {
        var line = new StringBuilder(",");

        //write header
        foreach (var knowledge in _knowledgeArray)
        {
            line.Append(knowledge).Append(',');
        }

        line.Append(Environment.NewLine);

        //now write each person
        foreach (var npc in _socialGraphs)
        {
            line.Append(npc.Name).Append(',');

            foreach (var knowledge in _knowledgeArray)
            {
                var knowledgeOfTopic = npc.Knowledge.Count(x => x.Topic == knowledge);
                line.Append(knowledgeOfTopic).Append(',');
            }

            line.Append(Environment.NewLine);
        }

        File.WriteAllText($"{SavePath}social_knowledge.csv", line.ToString().TrimEnd(',') + Environment.NewLine);

        //GRAPH
        //write header
        line = new StringBuilder(",");
        var knowledgeCount = new Dictionary<string, int>();
        foreach (var knowledge in _knowledgeArray)
        {
            line.Append(knowledge).Append(',');
            knowledgeCount[knowledge] = 0;
        }

        line.Append(Environment.NewLine);
        //now write each step
        for (long i = 0; i < _socialGraphs.Max(x => x.CurrentStep); i++)
        {
            foreach (var knowledge in _knowledgeArray)
            {
                // ReSharper disable once AccessToModifiedClosure
                foreach (var learningCount in _socialGraphs.Select(npc =>
                             npc.Knowledge.Count(x => x.Topic == knowledge && x.Step < i)))
                {
                    knowledgeCount[knowledge] += learningCount;
                }
            }

            line.Append(i).Append(',');
            foreach (var knowledge in _knowledgeArray)
            {
                line.Append(knowledgeCount[knowledge]).Append(',');
            }

            line.Length--;
            line.Append(Environment.NewLine);

            foreach (var key in knowledgeCount.Keys)
            {
                knowledgeCount[key] = 0;
            }
        }

        File.WriteAllText($"{SavePath}social_knowledge_graph.csv", line.ToString().TrimEnd(',') + Environment.NewLine);
    }


    private void ReportMatrix()
    {
        var line = new StringBuilder(",");

        //write header
        foreach (var npc in _socialGraphs)
        {
            line.Append(npc.Name).Append(',');
        }

        line.Append(Environment.NewLine);

        //now write each person
        foreach (var npc in _socialGraphs)
        {
            line.Append(npc.Name).Append(',');
            foreach (var connection in _socialGraphs)
            {
                if (connection.Id == npc.Id)
                {
                    line.Append(',');
                    continue;
                }

                var myRelationshipStatus =
                    npc.Connections.FirstOrDefault(x => x.Id == connection.Id)!.RelationshipStatus;
                // var otherNpcRelationshipStatus = this._socialGraphs.FirstOrDefault(x => x.Id == connection.Id).Connections
                //     .FirstOrDefault(y => y.Id == npc.Id).RelationshipStatus; 

                //line.Append(myRelationshipStatus).Append("::").Append(otherNpcRelationshipStatus).Append(',');

                line.Append(myRelationshipStatus).Append(',');
            }

            line.Append(Environment.NewLine);
        }

        File.WriteAllText($"{SavePath}social_matrix.csv", line.ToString().TrimEnd(',') + Environment.NewLine);
    }
}
