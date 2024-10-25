// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Ghosts.Api;
using NLog;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace ghosts.api.Infrastructure.ContentServices.OpenAi;

public class OpenAiConnectorService : IContentService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly OpenAIService _service;
    public bool IsReady { get; set; }

    public OpenAiConnectorService()
    {
        var apiKey = OpenAiHelpers.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            // _log.Warn("No OPEN_AI_API_KEY environment variable set. OpenAIConnectorService not enabled.");
            return;
        }

        HttpClientHandler handler;
        if (!string.IsNullOrEmpty(Program.ApplicationSettings.AnimatorSettings.Proxy))
        {
            handler = new HttpClientHandler()
            {
                Proxy = new WebProxy(Program.ApplicationSettings.AnimatorSettings.Proxy),
                UseProxy = true
            };
        }
        else
        {
            handler = new HttpClientHandler()
            {
                Proxy = new WebProxy(),
                UseProxy = false
            };
        }

        var client = new HttpClient(handler);

        _service = new OpenAIService(new OpenAiOptions
        {
            ApiKey = apiKey
        }, client);

        IsReady = true;
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem(prompt)
        };
        return await ExecuteQuery(messages);
    }

    //TODO: shouldn't this method save off every request and response somewhere?
    public async Task<string> ExecuteQuery(IList<ChatMessage> messages)
    {
        var completionResult = await _service.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = OpenAI.ObjectModels.Models.Gpt_4,
            //Model = OpenAI.GPT3.ObjectModels.Models.ChatGpt3_5Turbo,
            MaxTokens = 500 //optional
            //Temperature = 0.7 //optional
        });

        if (completionResult.Successful)
        {
            var resp = completionResult.Choices.First().Message.Content;
            _log.Trace(resp);
            return resp;
        }

        if (completionResult.Error != null)
        {
            _log.Warn(completionResult.Error.Message);
        }

        return string.Empty;
    }
}
