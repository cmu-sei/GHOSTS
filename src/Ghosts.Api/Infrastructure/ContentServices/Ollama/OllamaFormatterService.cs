// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using NLog;

namespace ghosts.api.Infrastructure.ContentServices.Ollama;

public class OllamaFormatterService : IFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private readonly OllamaConnectorService _connectorService;

    public OllamaFormatterService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;

        _connectorService = new OllamaConnectorService(_configuration);
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        return await _connectorService.ExecuteQuery(prompt);
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);

        var prompt = await File.ReadAllTextAsync("config/ContentServices/Ollama/GenerateTweet.txt");
        var messages = new StringBuilder();
        foreach (var p in prompt.Split(System.Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent[..3050]);
            messages.Append(s);
        }

        var tweetText = await _connectorService.ExecuteQuery(messages.ToString());
        var tries = 0;
        while (string.IsNullOrEmpty(tweetText))
        {
            tweetText = await _connectorService.ExecuteQuery(messages.ToString());
            tries++;
            if (tries > 5)
                return null;
        }

        var regArray = new[] { "\"activities\": \\[\"([^\"]+)\"", "\"activity\": \"([^\"]+)\"", "'activities': \\['([^\\']+)'\\]", "\"activities\": \\[\"([^\\']+)'\\]" };

        foreach (var reg in regArray)
        {
            var match = Regex.Match(tweetText, reg);
            if (match.Success)
            {
                // Extract the activity
                tweetText = match.Groups[1].Value;
                break;
            }
        }

        return tweetText;
    }

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        const string promptPath = "config/ContentServices/Ollama/GenerateNextAction.txt";
        var flattenedAgent = GenericContentHelpers.GetFlattenedNpc(npc);
        if (flattenedAgent.Length > 3050)
        {
            flattenedAgent = flattenedAgent[..3050];
        }

        _log.Trace($"{npc.NpcProfile.Name} with {history.Length} history records. Loading prompt from: {promptPath}");

        var prompt = await File.ReadAllTextAsync(promptPath);
        var messages = new StringBuilder();
        foreach (var p in prompt.Split(Environment.NewLine))
        {
            var s = p.Replace("[[flattenedAgent]]", flattenedAgent);
            s = s.Replace("[[history]]", history);
            messages.Append(s).Append(' ');
        }

        return await _connectorService.ExecuteQuery(messages.ToString());
    }
}
