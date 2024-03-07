// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ghosts.api.Areas.Animator.Infrastructure.ContentServices.Native;
using ghosts.api.Areas.Animator.Infrastructure.ContentServices.Ollama;
using ghosts.api.Areas.Animator.Infrastructure.ContentServices.OpenAi;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Extensions;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.ContentServices;

public class ContentCreationService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;
    private OpenAiFormatterService _openAiFormatterService;
    private OllamaFormatterService _ollamaFormatterService;

    public ContentCreationService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;

        if (_configuration.Source.ToLower() == "openai" && this._openAiFormatterService.IsReady)
            _openAiFormatterService = new OpenAiFormatterService();
        else if (_configuration.Source.ToLower() == "ollama")
            _ollamaFormatterService = new OllamaFormatterService(_configuration);
        
        _log.Trace($"Content service configured for {_configuration.Source} on {_configuration.Host} running {_configuration.Model}");
    }

    public async Task<string> GenerateNextAction(NpcRecord agent, string history)
    {
        var nextAction = string.Empty;
        try
        {
            if (_configuration.Source.ToLower() == "openai" && this._openAiFormatterService.IsReady)
            {
                nextAction = await this._openAiFormatterService.GenerateNextAction(agent, history).ConfigureAwait(false);
            }
            else if (_configuration.Source.ToLower() == "ollama")
            {
                nextAction = await this._ollamaFormatterService.GenerateNextAction(agent, history);
            }

            _log.Info($"{agent.NpcProfile.Name}'s next action is: {nextAction}");
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        return nextAction;
    }
    
    public async Task<string> GenerateTweet(NpcRecord agent)
    {
        string tweetText = null;

        try
        {
            if (_configuration.Source.ToLower() == "openai" && this._openAiFormatterService.IsReady)
            {
                tweetText = await this._openAiFormatterService.GenerateTweet(agent).ConfigureAwait(false);
            }
            else if (_configuration.Source.ToLower() == "ollama")
            {
                tweetText = await this._ollamaFormatterService.GenerateTweet(agent);
                
                var regArray = new [] {"\"activities\": \\[\"([^\"]+)\"", "\"activity\": \"([^\"]+)\"", "'activities': \\['([^\\']+)'\\]", "\"activities\": \\[\"([^\\']+)'\\]"} ;

                foreach (var reg in regArray)
                {
                    var match = Regex.Match(tweetText,reg);
                    if (match.Success)
                    {
                        // Extract the activity
                        tweetText = match.Groups[1].Value;
                        break;
                    }
                }
            }
            
            while (string.IsNullOrEmpty(tweetText))
            {
                tweetText = NativeContentFormatterService.GenerateTweet(agent);
            }

            tweetText = tweetText.ReplaceDoubleQuotesWithSingleQuotes(); // else breaks csv file, //TODO should replace this with a proper csv library

            _log.Info($"{agent.NpcProfile.Name} said: {tweetText}");
        }
        catch (Exception e)
        {
            _log.Info(e);
        }
        return tweetText;
    }
}