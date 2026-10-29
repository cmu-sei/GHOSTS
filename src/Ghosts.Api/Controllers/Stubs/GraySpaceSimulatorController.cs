// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Stubs;

/// <summary>
/// GraySpace Terrain API 1.3.0, answered in memory by GraySpaceSimulator, for development without Proxmox. It is off
/// unless TerrainProvider:EnableStub is true, as the Aspire AppHost sets it. Like GraySpace, it needs the bearer token
/// (TerrainProvider:Token), answers snake_case and reports errors as {"detail": ...}. See docs/core/api/terrain.md.
/// </summary>
[ApiController]
[Route("stubs/grayspace/api/v1")]
[SwaggerTag("GraySpace Terrain API 1.3.0 (simulated). The calls GHOSTS makes, answered in memory.")]
public class GraySpaceSimulatorController(GraySpaceSimulator sim, IOptions<TerrainProviderOptions> options) : ControllerBase
{
    /// <summary>The deployed base and its health.</summary>
    [HttpGet("base")]
    public Task<IActionResult> GetBase(CancellationToken ct) => Run(() => sim.GetBaseAsync(ct));

    /// <summary>Images, roles, enclave features and whether jobs run.</summary>
    [HttpGet("capabilities")]
    public Task<IActionResult> GetCapabilities(CancellationToken ct) => Run(() => sim.GetCapabilitiesAsync(ct));

    /// <summary>The stored enclaves (GraySpace: slices) and their versions.</summary>
    [HttpGet("slices")]
    public Task<IActionResult> ListSlices(CancellationToken ct) => Run(() => sim.ListSlicesAsync(ct));

    /// <summary>Store an enclave's spec; returns its version and findings.</summary>
    [HttpPut("slices/{slug}")]
    public Task<IActionResult> PutSlice(string slug, CancellationToken ct) => Run(async () => await sim.PutSliceAsync(await SpecAsync(slug, ct), ct));

    /// <summary>Findings for a spec; nothing is stored.</summary>
    [HttpPost("slices/{slug}/validate")]
    public Task<IActionResult> ValidateSlice(string slug, CancellationToken ct) => Run(async () => await sim.ValidateSliceAsync(await SpecAsync(slug, ct), ct));

    /// <summary>Build an instance of a stored enclave version: a create job.</summary>
    [HttpPost("instances")]
    public Task<IActionResult> CreateInstance(CancellationToken ct) =>
        Run(async () => await sim.CreateInstanceAsync(await BodyAsync<CreateInstanceRequest>(ct), ct), status: 202);

    /// <summary>An instance's state and, once ready, its manifest.</summary>
    [HttpGet("instances/{instanceId}")]
    public Task<IActionResult> GetInstance(string instanceId, CancellationToken ct) => Run(() => sim.GetInstanceAsync(instanceId, ct));

    /// <summary>Tear an instance down: a destroy job.</summary>
    [HttpDelete("instances/{instanceId}")]
    public Task<IActionResult> DestroyInstance(string instanceId, CancellationToken ct) => Run(() => sim.DestroyInstanceAsync(instanceId, ct), status: 202);

    /// <summary>A job's state, stage and attempt.</summary>
    [HttpGet("jobs/{jobId}")]
    public Task<IActionResult> GetJob(string jobId, CancellationToken ct) => Run(() => sim.GetJobAsync(jobId, ct));

    /// <summary>A job's log, as text. As in GraySpace, follow (the default) keeps the response open until the job ends.</summary>
    [HttpGet("jobs/{jobId}/log")]
    public async Task<IActionResult> GetJobLog(string jobId, [FromQuery] bool follow = true, CancellationToken ct = default)
    {
        if (Guard() is { } denied) return denied;
        try
        {
            while (follow && (await sim.GetJobAsync(jobId, ct)).State is JobState.Queued or JobState.Running)
                await Task.Delay(500, ct);
            return new ContentResult { Content = await sim.GetJobLogAsync(jobId, ct), ContentType = "text/plain", StatusCode = 200 };
        }
        catch (TerrainProviderException ex) { return Detail(ex.Status, ex.Detail); }
    }

    /// <summary>Run a failed or cancelled job again.</summary>
    [HttpPost("jobs/{jobId}/retry")]
    public Task<IActionResult> RetryJob(string jobId, CancellationToken ct) => Run(() => sim.RetryJobAsync(jobId, ct), status: 202);

    /// <summary>The map layer as GeoJSON; instance limits the enclave features to one instance.</summary>
    [HttpGet("map")]
    public async Task<IActionResult> GetMap([FromQuery] string instance, CancellationToken ct)
    {
        if (Guard() is { } denied) return denied;
        return new ContentResult { Content = await sim.GetMapAsync(instance, ct), ContentType = "application/json", StatusCode = 200 };
    }

    /// <summary>
    /// The /api/v1/events websocket: hello, then job.update, instance.update and feature.update as they happen; "ping"
    /// is answered with a pong. The token comes in the Authorization header or ?token=.
    /// </summary>
    [HttpGet("events")]
    public async Task Events(CancellationToken ct)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest || Guard(Request.Query["token"]) != null)
        {
            HttpContext.Response.StatusCode = HttpContext.WebSockets.IsWebSocketRequest ? 403 : 400;
            return;
        }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var events = sim.Subscribe();
        try
        {
            await SendAsync(socket, """{"type":"hello","schema":"grayspace-events-v1"}""", ct);
            var buffer = new byte[1024];
            var receive = socket.ReceiveAsync(buffer, ct);
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sim.Tick();
                while (events.TryRead(out var message)) await SendAsync(socket, message, ct);
                if (await Task.WhenAny(receive, Task.Delay(1000, ct)) != receive) continue;
                var got = await receive;
                if (got.MessageType == WebSocketMessageType.Close) break;
                if (Encoding.UTF8.GetString(buffer, 0, got.Count) == "ping") await SendAsync(socket, """{"type":"pong"}""", ct);
                receive = socket.ReceiveAsync(buffer, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            sim.Unsubscribe(events);
        }
    }

    private static Task SendAsync(WebSocket socket, string text, CancellationToken ct) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    private async Task<IActionResult> Run<T>(Func<Task<T>> call, int status = 200)
    {
        if (Guard() is { } denied) return denied;
        try
        {
            return new ContentResult { Content = JsonSerializer.Serialize(await call(), GraySpaceClient.Wire), ContentType = "application/json", StatusCode = status };
        }
        catch (TerrainProviderException ex) when (ex.Findings != null)
        {
            var detail = new JsonObject { ["message"] = ex.Detail, ["findings"] = JsonNode.Parse(JsonSerializer.Serialize(ex.Findings, GraySpaceClient.Wire)) };
            return new ContentResult { Content = new JsonObject { ["detail"] = detail }.ToJsonString(), ContentType = "application/json", StatusCode = ex.Status };
        }
        catch (TerrainProviderException ex)
        {
            return Detail(ex.Status, ex.Detail);
        }
        catch (JsonException ex)
        {
            var detail = new JsonArray(new JsonObject { ["loc"] = new JsonArray("body"), ["msg"] = ex.Message, ["type"] = "json_invalid" });
            return new ContentResult { Content = new JsonObject { ["detail"] = detail }.ToJsonString(), ContentType = "application/json", StatusCode = 422 };
        }
    }

    private async Task<SliceSpec> SpecAsync(string slug, CancellationToken ct)
    {
        var spec = await BodyAsync<SliceSpec>(ct);
        if (spec.Slug != slug)
            throw new TerrainProviderException(422, "invalid_request", $"The spec's slug '{spec.Slug}' is not the path's '{slug}'");
        return spec;
    }

    private async Task<T> BodyAsync<T>(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        return JsonSerializer.Deserialize<T>(await reader.ReadToEndAsync(ct), GraySpaceClient.Wire)
               ?? throw new JsonException("The body is empty");
    }

    /// <summary>GraySpace's guard: off → 404; no token configured → 503; missing or wrong bearer token → 401.</summary>
    private IActionResult Guard(string queryToken = null)
    {
        if (!options.Value.EnableStub) return Detail(404, "The GraySpace simulator is off (TerrainProvider:EnableStub)");
        var token = options.Value.Token;
        if (string.IsNullOrEmpty(token)) return Detail(503, "GRAYSPACE_API_TOKEN is not set on this GraySpace; protected routes are disabled");
        var header = Request.Headers.Authorization.ToString();
        if (header != $"Bearer {token}" && queryToken != token)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Detail(401, "Missing or invalid bearer token");
        }
        return null;
    }

    private static ContentResult Detail(int status, string detail) =>
        new() { Content = new JsonObject { ["detail"] = detail }.ToJsonString(), ContentType = "application/json", StatusCode = status };
}
