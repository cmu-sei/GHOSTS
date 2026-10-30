// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Model;

/// <summary>
/// POST /model/chat/completions — the only path from the agent to a model. It swaps the configured name for the
/// server's model id, adds the server's key, and relays the answer as it arrives, so streamed events stream through.
/// Errors keep the OpenAI shape, so the agent reports them as the model's.
/// </summary>
public class ChatCompletionsEndpoint : IEndpoint
{
    public const string Client = "models";

    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/model/chat/completions", async (HttpContext http, IOptions<ModelOptions> options, IHttpClientFactory clients) =>
        {
            var ct = http.RequestAborted;
            if (await JsonNode.ParseAsync(http.Request.Body, cancellationToken: ct) is not JsonObject body)
                return Error(StatusCodes.Status400BadRequest, "The request body must be a JSON object.");
            var name = body["model"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            var entry = options.Value.Models.FirstOrDefault(m => m.Name == name);
            if (entry is null)
                return Error(StatusCodes.Status404NotFound,
                    $"No model named '{name}'. Configured: {string.Join(", ", options.Value.Models.Select(m => m.Name))}.");

            body["model"] = entry.Model;
            var request = new HttpRequestMessage(HttpMethod.Post, $"{entry.BaseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(entry.ApiKeyEnv) && Environment.GetEnvironmentVariable(entry.ApiKeyEnv) is { Length: > 0 } key)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

            try
            {
                var upstream = await clients.CreateClient(Client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                return new Relay(upstream);
            }
            catch (HttpRequestException e)
            {
                return Error(StatusCodes.Status502BadGateway, $"Can't reach the model server at {entry.BaseUrl}: {e.Message}");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return Error(StatusCodes.Status504GatewayTimeout,
                    $"The model server at {entry.BaseUrl} didn't answer within {options.Value.TimeoutMinutes} minutes.");
            }
        }).WithTags(tag);

    private static IResult Error(int status, string message) => Results.Json(new { error = new { message } }, statusCode: status);

    /// <summary>Copies the upstream status, content type and body to the caller, chunk by chunk.</summary>
    private sealed class Relay(HttpResponseMessage upstream) : IResult
    {
        public async Task ExecuteAsync(HttpContext http)
        {
            using (upstream)
            {
                http.Response.StatusCode = (int)upstream.StatusCode;
                if (upstream.Content.Headers.ContentType is { } type)
                    http.Response.ContentType = type.ToString();
                await using var stream = await upstream.Content.ReadAsStreamAsync(http.RequestAborted);
                await stream.CopyToAsync(http.Response.Body, http.RequestAborted);
            }
        }
    }
}
