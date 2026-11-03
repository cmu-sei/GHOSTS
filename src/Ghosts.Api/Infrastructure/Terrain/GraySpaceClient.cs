// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>GHOSTS's client for GraySpace Terrain API 1.3.0. See docs/core/api/terrain.md.</summary>
public class GraySpaceClient(HttpClient http, IOptions<TerrainProviderOptions> options) : IGraySpace
{
    /// <summary>GraySpace's wire format: snake_case properties, dictionary keys as they are, nulls left out unless a field says otherwise.</summary>
    public static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<DeployedBase> GetBaseAsync(CancellationToken ct) => SendAsync<DeployedBase>(HttpMethod.Get, "api/v1/base", null, ct);

    public Task<GraySpaceCapabilities> GetCapabilitiesAsync(CancellationToken ct) =>
        SendAsync<GraySpaceCapabilities>(HttpMethod.Get, "api/v1/capabilities", null, ct);

    public Task<System.Collections.Generic.List<SliceSummary>> ListSlicesAsync(CancellationToken ct) =>
        SendAsync<System.Collections.Generic.List<SliceSummary>>(HttpMethod.Get, "api/v1/slices", null, ct);

    public Task<PutSliceResult> PutSliceAsync(SliceSpec spec, CancellationToken ct) =>
        SendAsync<PutSliceResult>(HttpMethod.Put, $"api/v1/slices/{Uri.EscapeDataString(spec.Slug)}", spec, ct);

    public Task<ValidateResult> ValidateSliceAsync(SliceSpec spec, CancellationToken ct) =>
        SendAsync<ValidateResult>(HttpMethod.Post, $"api/v1/slices/{Uri.EscapeDataString(spec.Slug)}/validate", spec, ct);

    public Task<JobAccepted> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct) =>
        SendAsync<JobAccepted>(HttpMethod.Post, "api/v1/instances", request, ct);

    public Task<InstanceView> GetInstanceAsync(string instanceId, CancellationToken ct) =>
        SendAsync<InstanceView>(HttpMethod.Get, $"api/v1/instances/{Uri.EscapeDataString(instanceId)}", null, ct);

    public Task<JobAccepted> DestroyInstanceAsync(string instanceId, CancellationToken ct) =>
        SendAsync<JobAccepted>(HttpMethod.Delete, $"api/v1/instances/{Uri.EscapeDataString(instanceId)}", null, ct);

    public Task<JobRecord> GetJobAsync(string jobId, CancellationToken ct) =>
        SendAsync<JobRecord>(HttpMethod.Get, $"api/v1/jobs/{Uri.EscapeDataString(jobId)}", null, ct);

    /// <summary>The log so far. follow=false, since GraySpace otherwise streams it until the job ends.</summary>
    public Task<string> GetJobLogAsync(string jobId, CancellationToken ct) =>
        SendTextAsync(HttpMethod.Get, $"api/v1/jobs/{Uri.EscapeDataString(jobId)}/log?follow=false", null, ct);

    public Task<JobAccepted> RetryJobAsync(string jobId, CancellationToken ct) =>
        SendAsync<JobAccepted>(HttpMethod.Post, $"api/v1/jobs/{Uri.EscapeDataString(jobId)}/retry", null, ct);

    public Task<string> GetMapAsync(string instanceId, CancellationToken ct) =>
        SendTextAsync(HttpMethod.Get, string.IsNullOrEmpty(instanceId) ? "api/v1/map" : $"api/v1/map?instance={Uri.EscapeDataString(instanceId)}", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken ct) =>
        JsonSerializer.Deserialize<T>(await SendTextAsync(method, path, body, ct), Wire);

    private async Task<string> SendTextAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        var baseUrl = options.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            throw new TerrainProviderException(0, "not_configured", "TerrainProvider:BaseUrl is not set");

        using var request = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Token ?? string.Empty);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, body.GetType(), Wire), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new TerrainProviderException(0, "unreachable", $"GraySpace at {baseUrl} did not answer: {ex.Message}");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode) return text;
            var status = (int)response.StatusCode;
            throw new TerrainProviderException(status, Code(status), Detail(text)) { Findings = Findings(text) };
        }
    }

    private static string Code(int status) => status switch
    {
        401 or 403 => "unauthorized",
        404 => "not_found",
        409 => "conflict",
        422 => "invalid_request",
        503 => "unavailable",
        _ => "http_error"
    };

    /// <summary>
    /// FastAPI's error body: {"detail": "..."}; for a 422 {"detail": [{"loc": [...], "msg": "..."}]}; or, for a create
    /// GraySpace refuses, {"detail": {"message": "...", "findings": [...]}}.
    /// </summary>
    private static string Detail(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("detail", out var detail)) return text;
            if (detail.ValueKind == JsonValueKind.String) return detail.GetString();
            if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("message", out var message)) return message.GetString();
            if (detail.ValueKind == JsonValueKind.Array)
                return string.Join("; ", detail.EnumerateArray().Select(e =>
                {
                    var loc = e.TryGetProperty("loc", out var l) ? string.Join(".", l.EnumerateArray().Select(x => x.ToString())) : "";
                    var msg = e.TryGetProperty("msg", out var m) ? m.GetString() : e.ToString();
                    return string.IsNullOrEmpty(loc) ? msg : $"{loc}: {msg}";
                }));
            return detail.ToString();
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static System.Collections.Generic.List<Finding> Findings(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Object
                   && detail.TryGetProperty("findings", out var findings)
                ? findings.Deserialize<System.Collections.Generic.List<Finding>>(Wire)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
