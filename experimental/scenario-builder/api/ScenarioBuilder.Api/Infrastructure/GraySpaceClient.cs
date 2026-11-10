// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>A GraySpace call that didn't succeed, as the builder API answers it: a status, a code and a message, and GraySpace's own detail.</summary>
public sealed class GraySpaceException(int status, string code, string message, JsonNode? detail = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public JsonNode? Detail { get; } = detail;
}

/// <summary>
/// Calls the connected GraySpace's Terrain API 1.4.0 with its token as a bearer. Answers come back as GraySpace wrote
/// them; a failed call throws <see cref="GraySpaceException"/>.
/// </summary>
public sealed class GraySpaceClient(BuilderDb db, IHttpClientFactory clients)
{
    public const string Client = "grayspace";

    public async Task<JsonNode?> Json(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        var text = await Text(method, path, body, ct);
        return text.Length == 0 ? null : JsonNode.Parse(text);
    }

    public Task<JsonNode?> Get(string path, CancellationToken ct) => Json(HttpMethod.Get, path, null, ct);

    public async Task<string> Text(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        var connection = await db.GraySpaceConnections.FirstOrDefaultAsync(ct)
                         ?? throw new GraySpaceException(StatusCodes.Status409Conflict, "NOT_CONNECTED",
                             "GraySpace isn't connected. An admin connects it under Connection on the Deploy to GraySpace page.");
        return await Send(connection.Url, connection.Token, method, path, body, ct);
    }

    /// <summary>One call to the GraySpace at <paramref name="url"/>: Connect tries an address and token with it before saving them.</summary>
    public async Task<string> Send(string url, string token, HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{url}/api/v1/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        int status;
        string text;
        try
        {
            using var response = await clients.CreateClient(Client).SendAsync(request, ct);
            status = (int)response.StatusCode;
            text = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new GraySpaceException(StatusCodes.Status502BadGateway, "GRAYSPACE_UNREACHABLE",
                $"GraySpace at {url} didn't answer ({e.Message}). Check the address, and that the builder's API can reach it.");
        }

        if (status is >= 200 and < 300)
            return text;
        var detail = Detail(text);
        throw status switch
        {
            401 or 403 => new GraySpaceException(StatusCodes.Status502BadGateway, "GRAYSPACE_TOKEN_REJECTED",
                $"GraySpace at {url} refused the token. An admin reconnects with its GRAYSPACE_API_TOKEN.", detail),
            >= 500 => new GraySpaceException(StatusCodes.Status502BadGateway, "GRAYSPACE_FAILED",
                $"GraySpace failed: {Describe(detail, status)}", detail),
            _ => new GraySpaceException(status, "GRAYSPACE_REFUSED", $"GraySpace refused it: {Describe(detail, status)}", detail)
        };
    }

    /// <summary>GraySpace's <c>detail</c>, as FastAPI writes it; or the text, when it isn't JSON.</summary>
    private static JsonNode? Detail(string text)
    {
        try
        {
            return JsonNode.Parse(text) is JsonObject o && o.TryGetPropertyValue("detail", out var detail) ? detail : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return text.Length == 0 ? null : JsonValue.Create(text);
        }
    }

    /// <summary>A detail in a sentence: its text, its validation messages, or its findings' messages.</summary>
    private static string Describe(JsonNode? detail, int status) => detail switch
    {
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonArray errors => string.Join("; ", errors.Select(e => $"{string.Join('.', e?["loc"]?.AsArray().Select(l => l?.ToString()) ?? [])}: {e?["msg"]}")),
        JsonObject { } o when o["findings"] is JsonArray findings =>
            $"{(string?)o["message"] ?? $"{findings.Count} finding(s)"}: {string.Join("; ", findings.Select(f => $"{f?["path"]}: {f?["message"]}"))}",
        JsonObject { } o when o["message"] is JsonValue m => m.ToString(),
        _ => $"HTTP {status}."
    };
}
