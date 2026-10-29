// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NLog;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>
/// What GHOSTS does with a GraySpace /api/v1/events message: a job or instance update refreshes the terrain now
/// rather than at the next poll, and every event but hello and pong goes to the frontend as TerrainEvent on the
/// execution hub, so the map applies feature updates and the Terrain tab follows builds.
/// </summary>
public class GraySpaceEventHandler(IServiceScopeFactory scopes, IHubContext<ExecutionHub> hub)
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task HandleAsync(string message, CancellationToken ct)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(message);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            _log.Debug($"Ignoring a GraySpace event that is not JSON: {message}");
            return;
        }

        var type = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is null or "hello" or "pong") return;

        if (type is "job.update" or "instance.update")
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ITerrainService>().PollAsync(ct);
        }

        await hub.Clients.All.SendAsync("TerrainEvent", root, ct);
    }
}

/// <summary>
/// Holds a connection to GraySpace's /api/v1/events while a provider is configured: the bearer token in the
/// Authorization header, a ping every 30 seconds, and a reconnect with backoff when the socket drops. TerrainStatusPoller
/// still polls, so a lost connection only slows updates down.
/// </summary>
public class GraySpaceEventsRelay(IOptions<TerrainProviderOptions> options, GraySpaceEventHandler handler) : BackgroundService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan Ping = TimeSpan.FromSeconds(30);

    /// <summary>How often a ping goes out while the connection is open.</summary>
    public TimeSpan PingInterval { get; init; } = Ping;

    public static Uri EventsUri(string baseUrl)
    {
        var uri = new UriBuilder(baseUrl.TrimEnd('/') + "/api/v1/events");
        uri.Scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        uri.Port = uri.Uri.IsDefaultPort ? -1 : uri.Port;
        return uri.Uri;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl)) return;

        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("Authorization", $"Bearer {settings.Token}");
                await socket.ConnectAsync(EventsUri(settings.BaseUrl), stoppingToken);
                _log.Info($"Following GraySpace events at {EventsUri(settings.BaseUrl)}");
                delay = TimeSpan.FromSeconds(5);
                await ReadAsync(socket, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.Warn($"GraySpace events: {ex.Message}; reconnecting in {delay.TotalSeconds:F0}s");
            }

            await Task.Delay(delay, stoppingToken);
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }
    }

    private async Task ReadAsync(ClientWebSocket socket, CancellationToken ct)
    {
        // A receive must not be cancelled to make time for a ping: cancelling a WebSocket receive aborts the socket.
        // Pings go out from their own loop instead; one that fails aborts the socket, which ends the receive below.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pinger = PingAsync(socket, stop.Token);
        try
        {
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct);
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) return;
                await handler.HandleAsync(Encoding.UTF8.GetString(message.ToArray()), ct);
            }
        }
        finally
        {
            stop.Cancel();
            try { await pinger; } catch (OperationCanceledException) { }
        }
    }

    private async Task PingAsync(ClientWebSocket socket, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                await Task.Delay(PingInterval, ct);
                await socket.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Warn($"GraySpace events: ping failed ({ex.Message}); reconnecting");
            socket.Abort();
        }
    }
}
