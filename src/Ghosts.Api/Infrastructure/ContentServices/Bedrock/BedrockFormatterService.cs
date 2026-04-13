// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using NLog;

namespace Ghosts.Api.Infrastructure.ContentServices.Bedrock;

public class BedrockFormatterService : IFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly BedrockConnectorService _connectorService;

    public BedrockFormatterService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _connectorService = new BedrockConnectorService(configuration);
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        return await _connectorService.ExecuteQuery(prompt);
    }

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);
        if (flattenedAgent.Length > 3050)
            flattenedAgent = flattenedAgent[..3050];

        var prompt = $"You are simulating a computer user. Given this user profile: {flattenedAgent} " +
                     $"and their recent activity history: {history} — what is the most likely next action they would take? " +
                     "Respond with a single short action description.";

        return await _connectorService.ExecuteQuery(prompt);
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);
        if (flattenedAgent.Length > 1000)
            flattenedAgent = flattenedAgent[..1000];

        var prompt = $"You are simulating a social media user with this profile: {flattenedAgent} " +
                     "Write a single realistic tweet (under 280 characters) they might post. " +
                     "Respond with only the tweet text, no quotes or commentary.";

        return await _connectorService.ExecuteQuery(prompt);
    }
}
