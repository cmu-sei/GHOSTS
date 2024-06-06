// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure;
using ghosts.api.Infrastructure.ContentServices.Ollama;
using ghosts.api.Infrastructure.ContentServices.OpenAi;
using ghosts.api.Infrastructure.ContentServices.Shadows;
using ghosts.api.Infrastructure.Models;
using NLog;

namespace ghosts.api.Infrastructure.ContentServices;

public class ContentCreationService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private OpenAiFormatterService _openAiFormatterService;
    private OllamaFormatterService _ollamaFormatterService;
    private ShadowsFormatterService _shadowsFormatterService;
    public IFormatterService FormatterService;

    public ContentCreationService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;

        if (_configuration.Source.ToLower() == "openai" && this._openAiFormatterService.IsReady)
        {
            _openAiFormatterService = new OpenAiFormatterService();
            this.FormatterService = _openAiFormatterService;
        }
        else if (_configuration.Source.ToLower() == "ollama")
        {
            _ollamaFormatterService = new OllamaFormatterService(_configuration);
            this.FormatterService = _ollamaFormatterService;
        }
        else if (_configuration.Source.ToLower() == "shadows")
        {
            _shadowsFormatterService = new ShadowsFormatterService(_configuration);
            this.FormatterService = _shadowsFormatterService;
        }
        
        _log.Trace($"Content service configured for {_configuration.Source} on {_configuration.Host} running {_configuration.Model}");
    }

    public async Task<string> GenerateNextAction(NpcRecord agent, string history)
    {
        var nextAction = await this.FormatterService.GenerateNextAction(agent, history);
        _log.Info($"{agent.NpcProfile.Name}'s next action is: {nextAction}");
        return nextAction;
    }
    
    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        var tweetText = await this.FormatterService.GenerateTweet(npc);
        _log.Info($"{npc.NpcProfile.Name} said: {tweetText}");
        return tweetText;
    }

}