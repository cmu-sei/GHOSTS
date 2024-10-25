// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Extensions;
using Ghosts.Api.Infrastructure;
using Newtonsoft.Json;
using NLog;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ghosts.api.Infrastructure.ContentServices.Ollama;

public class OllamaConnectorService : IContentService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings _configuration;

    public OllamaConnectorService(ApplicationSettings.AnimatorSettingsDetail.ContentEngineSettings configuration)
    {
        _configuration = configuration;
        _configuration.Host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ??
                              configuration.Host;
        _configuration.Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ??
                               configuration.Model;
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        return await ExecuteQuery(_configuration.Model, prompt);
    }

    public async Task<string> ExecuteQuery(string modelName, string prompt, string system = null,
        string template = null, string context = null, string options = null, Action<string> callback = null)
    {
        Dictionary<string, string> payload = null;
        try
        {
            var url = $"{_configuration.Host}/api/generate";
            payload = new Dictionary<string, string>
            {
                { "model", modelName },
                { "prompt", prompt },
                { "system", system },
                { "template", template },
                { "context", context },
                { "options", options }
            };

            payload = payload.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value);
            using var client = new HttpClient();
            client.Timeout = new TimeSpan(0, 0, 60);
            using var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var fullResponse = new StringBuilder();

            using var streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
            while (await streamReader.ReadLineAsync() is { } line)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    try
                    {
                        dynamic json = JsonConvert.DeserializeObject(line);
                        if (json != null && json["done"] != true)
                        {
                            fullResponse.Append(json["response"]);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Info($"Ollama response was malformed: {e}");
                        fullResponse.Append(line);
                    }
                }
            }

            return fullResponse.ToString();
        }
        catch (Exception ex)
        {
            _log.Error($"Ollama threw an exception: {ex.Message}: {ex.StackTrace} on configuration {_configuration.Host} {_configuration.Model} with payload {payload.ToSafeString()}");
            return null;
        }
    }
}
