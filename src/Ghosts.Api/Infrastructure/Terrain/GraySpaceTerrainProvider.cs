// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>GHOSTS's client for the GraySpace Terrain API, version 1. See docs/core/api/terrain.md.</summary>
public class GraySpaceTerrainProvider(HttpClient http, IOptions<TerrainProviderOptions> options) : ITerrainProvider
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<List<TerrainBase>> ListBasesAsync(CancellationToken ct) =>
        SendAsync<List<TerrainBase>>(HttpMethod.Get, "api/v1/bases", null, ct);

    public Task<TerrainBaseSnapshot> GetBaseSnapshotAsync(string baseId, CancellationToken ct) =>
        SendAsync<TerrainBaseSnapshot>(HttpMethod.Get, $"api/v1/bases/{Uri.EscapeDataString(baseId)}/snapshot", null, ct);

    public Task<List<TerrainEnclave>> ListEnclavesAsync(CancellationToken ct) =>
        SendAsync<List<TerrainEnclave>>(HttpMethod.Get, "api/v1/enclaves", null, ct);

    public Task<TerrainInstance> CreateInstanceAsync(CreateTerrainInstance request, CancellationToken ct) =>
        SendAsync<TerrainInstance>(HttpMethod.Post, "api/v1/instances", request, ct);

    public Task<TerrainInstance> GetInstanceAsync(string instanceId, CancellationToken ct) =>
        SendAsync<TerrainInstance>(HttpMethod.Get, $"api/v1/instances/{Uri.EscapeDataString(instanceId)}", null, ct);

    public Task<TerrainManifest> GetManifestAsync(string instanceId, CancellationToken ct) =>
        SendAsync<TerrainManifest>(HttpMethod.Get, $"api/v1/instances/{Uri.EscapeDataString(instanceId)}/manifest", null, ct);

    public Task<TerrainInstance> DeleteInstanceAsync(string instanceId, CancellationToken ct) =>
        SendAsync<TerrainInstance>(HttpMethod.Delete, $"api/v1/instances/{Uri.EscapeDataString(instanceId)}", null, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken ct)
    {
        var baseUrl = options.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            throw new TerrainProviderException(0, "not_configured", "TerrainProvider:BaseUrl is not set");

        using var request = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        request.Headers.Add("X-Api-Key", options.Value.ApiKey ?? string.Empty);
        request.Headers.Add("Accept", "application/json");
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            TerrainError error = null;
            try { error = JsonSerializer.Deserialize<TerrainError>(text, Json); } catch (JsonException) { }
            throw new TerrainProviderException((int)response.StatusCode, error?.Code ?? "http_error", error?.Message ?? text);
        }

        return JsonSerializer.Deserialize<T>(text, Json);
    }
}
