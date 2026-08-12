// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Extensions;
using NLog;

namespace Ghosts.Api.Infrastructure.ContentServices.Ollama;

/// <summary>
/// Minimal client for Ollama's /api/chat endpoint. Unlike OllamaConnectorService (which posts a
/// single prompt to /api/generate), this supports multi-turn messages and JSON-schema constrained
/// output, which is what makes small local models return reliably parseable answers.
/// </summary>
public class OllamaChatService(string host, IHttpClientFactory httpClientFactory) : IChatService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Regex ThinkBlock = new(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private readonly string _host = host.TrimEnd('/');

    /// <summary>
    /// Sends a chat completion request and returns the assistant message content.
    /// </summary>
    /// <param name="model">Ollama model name, e.g. gemma3:4b</param>
    /// <param name="system">System prompt</param>
    /// <param name="turns">Conversation turns, roles are user or assistant</param>
    /// <param name="format">Optional JSON schema object that constrains the response</param>
    /// <param name="temperature">Sampling temperature</param>
    /// <param name="maxTokens">Response cap (num_predict)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<string> Chat(string model, string system, IEnumerable<ChatTurn> turns, object format,
        double temperature, int maxTokens, CancellationToken ct)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(system))
            messages.Add(new { role = "system", content = system });

        messages.AddRange(turns.Select(t => (object)new { role = t.Role, content = t.Content }));

        var payload = new Dictionary<string, object>
        {
            { "model", model },
            { "messages", messages },
            { "stream", false },
            { "options", new { temperature, num_predict = maxTokens } },
            // Reasoning models (gemma4, qwen3, gpt-oss) otherwise spend the whole token budget
            // thinking and return an empty message
            { "think", false }
        };

        if (format != null)
            payload.Add("format", format);

        var (body, failure) = await Post(payload, ct);

        if (failure != null && failure.Contains("think", StringComparison.OrdinalIgnoreCase))
        {
            // Older Ollama builds reject the think field outright
            payload.Remove("think");
            (body, failure) = await Post(payload, ct);
        }

        if (failure != null)
        {
            _log.Error($"Ollama chat failed on {_host} with model {model.ToSafeLogValue()}: {failure.ToSafeLogValue()}");
            throw new InvalidOperationException($"Ollama ({_host}, {model}): {failure}");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
            return Clean(text);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or JsonException)
        {
            _log.Error($"Ollama chat response was malformed: {body.ToSafeLogValue()}");
            throw new InvalidOperationException($"Ollama ({_host}, {model}) returned an unexpected response.");
        }
    }

    /// <summary>
    /// Posts to /api/chat, returning the body on success or an error description on failure.
    /// </summary>
    private async Task<(string Body, string Failure)> Post(Dictionary<string, object> payload, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(180);

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{_host}/api/chat", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        return response.IsSuccessStatusCode
            ? (body, null)
            : (null, TryGetError(body) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}");
    }

    /// <summary>
    /// Returns the models installed on the Ollama host.
    /// </summary>
    public async Task<IEnumerable<string>> GetModels(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        using var response = await client.GetAsync($"{_host}/api/tags", ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("models")
            .EnumerateArray()
            .Select(m => m.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name)
            .ToList();
    }

    /// <summary>
    /// Some local models emit reasoning inline. Strip it so it never reaches the operator.
    /// </summary>
    private static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return ThinkBlock.Replace(text, string.Empty).Trim();
    }

    private static string TryGetError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
