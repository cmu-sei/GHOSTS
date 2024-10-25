// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.ContentServices.Ollama;
using ghosts.api.Infrastructure.ContentServices.OpenAi;
using ghosts.api.Infrastructure.ContentServices.Shadows;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using NLog;

namespace ghosts.api.Infrastructure.ContentServices;

public class ContentCreationService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private readonly OpenAiFormatterService _openAiFormatterService;
    private readonly OllamaFormatterService _ollamaFormatterService;
    private readonly ShadowsFormatterService _shadowsFormatterService;
    public IFormatterService FormatterService;

    public ContentCreationService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;

        if (_configuration.Source.Equals("openai", StringComparison.CurrentCultureIgnoreCase) && _openAiFormatterService.IsReady)
        {
            _openAiFormatterService = new OpenAiFormatterService();
            FormatterService = _openAiFormatterService;
        }
        else if (_configuration.Source.Equals("ollama", StringComparison.CurrentCultureIgnoreCase))
        {
            _ollamaFormatterService = new OllamaFormatterService(_configuration);
            FormatterService = _ollamaFormatterService;
        }
        else if (_configuration.Source.Equals("shadows", StringComparison.CurrentCultureIgnoreCase))
        {
            _shadowsFormatterService = new ShadowsFormatterService(_configuration);
            FormatterService = _shadowsFormatterService;
        }

        _log.Trace($"Content service configured for {_configuration.Source} on {_configuration.Host} running {_configuration.Model}");
    }

    public async Task<string> GenerateNextAction(NpcRecord agent, string history)
    {
        var nextAction = await FormatterService.GenerateNextAction(agent, history);
        _log.Info($"{agent.NpcProfile.Name}'s next action is: {nextAction}");
        return nextAction;
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var tweetText = await FormatterService.GenerateTweet(npc);
        _log.Info($"{npc.NpcProfile.Name} said: {tweetText}");
        return tweetText;
    }

}
