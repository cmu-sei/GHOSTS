// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// GraySpace's /api/v1/events reach GHOSTS: a job or instance update refreshes the terrain at once instead of at the
/// next poll, and every event is relayed to the frontend as TerrainEvent, so the map and the Terrain tab follow it.
/// </summary>
public class GraySpaceEventsTests
{
    private readonly Mock<ITerrainService> _terrain = new();
    private readonly Mock<IClientProxy> _all = new();
    private readonly List<(string Method, object?[] Args)> _sent = [];

    public GraySpaceEventsTests() =>
        _all.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((m, a, _) => _sent.Add((m, a)))
            .Returns(Task.CompletedTask);

    [Theory]
    [InlineData("""{"type":"job.update","id":"j-1","kind":"create_instance","target":"g7","state":"running","stage":"apply","attempt":"1/6","error":null,"timestamp":"2026-10-29T09:00:00Z"}""")]
    [InlineData("""{"type":"instance.update","id":"g7","state":"ready","error":null,"timestamp":"2026-10-29T09:00:30Z"}""")]
    public async Task A_job_or_instance_update_refreshes_the_terrain_and_is_relayed(string message)
    {
        await Handler().HandleAsync(message, default);

        _terrain.Verify(t => t.PollAsync(It.IsAny<CancellationToken>()), Times.Once);
        var (method, args) = Assert.Single(_sent);
        Assert.Equal("TerrainEvent", method);
        Assert.Equal(JsonDocument.Parse(message).RootElement.GetProperty("type").GetString(), ((JsonElement)args[0]!).GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_feature_update_is_relayed_without_a_refresh()
    {
        await Handler().HandleAsync("""{"type":"feature.update","id":"lum-chi:are-ash","kind":"link","properties":{"state":"down"},"timestamp":"2026-10-29T09:01:00Z"}""", default);

        _terrain.Verify(t => t.PollAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("down", ((JsonElement)Assert.Single(_sent).Args[0]!).GetProperty("properties").GetProperty("state").GetString());
    }

    [Theory]
    [InlineData("""{"type":"hello","schema":"grayspace-events-v1"}""")]
    [InlineData("""{"type":"pong"}""")]
    [InlineData("not json")]
    public async Task Hello_pong_and_noise_are_not_relayed(string message)
    {
        await Handler().HandleAsync(message, default);

        Assert.Empty(_sent);
        _terrain.Verify(t => t.PollAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("http://gs:8000", "ws://gs:8000/api/v1/events")]
    [InlineData("https://grayspace.range.local/", "wss://grayspace.range.local/api/v1/events")]
    [InlineData("http://localhost:5000/stubs/grayspace", "ws://localhost:5000/stubs/grayspace/api/v1/events")]
    public void The_events_socket_is_beside_the_api(string baseUrl, string expected) =>
        Assert.Equal(expected, GraySpaceEventsRelay.EventsUri(baseUrl).ToString());

    [Fact]
    public async Task A_quiet_connection_stays_open_and_is_kept_alive_with_pings()
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        var accepted = 0;
        var pings = 0;
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                Interlocked.Increment(ref accepted);
                var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                await socket.SendAsync(Encoding.UTF8.GetBytes("""{"type":"hello","schema":"grayspace-events-v1"}"""), WebSocketMessageType.Text, true, default);
                var buffer = new byte[256];
                while (socket.State == WebSocketState.Open)
                {
                    var got = await socket.ReceiveAsync(buffer, default);
                    if (Encoding.UTF8.GetString(buffer, 0, got.Count) == "ping") Interlocked.Increment(ref pings);
                }
            }
        });
        var relay = new GraySpaceEventsRelay(Options.Create(new TerrainProviderOptions { BaseUrl = $"http://localhost:{port}", Token = "t" }), Handler())
        {
            PingInterval = TimeSpan.FromMilliseconds(200)
        };

        await relay.StartAsync(default);
        await Task.Delay(1500);
        await relay.StopAsync(default);

        Assert.Equal(1, accepted);
        Assert.True(pings >= 3, $"{pings} pings");
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private GraySpaceEventHandler Handler()
    {
        var services = new ServiceCollection().AddSingleton(_terrain.Object).BuildServiceProvider();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(_all.Object);
        var hub = new Mock<IHubContext<ExecutionHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return new GraySpaceEventHandler(services.GetRequiredService<IServiceScopeFactory>(), hub.Object);
    }
}
