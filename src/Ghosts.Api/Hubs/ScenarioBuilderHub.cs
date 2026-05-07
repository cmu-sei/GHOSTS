// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Hubs;

public class ScenarioBuilderHub : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly ConnectionMapping<string> _connections = new();
    private readonly IScenarioService _scenarioService;

    public ScenarioBuilderHub(IScenarioService scenarioService)
    {
        _scenarioService = scenarioService;
    }

    public override Task OnConnectedAsync()
    {
        var scenarioId = Context.GetHttpContext()?.Request.Query["scenarioId"].ToString() ?? "all";
        _connections.Add(scenarioId, Context.ConnectionId);
        _log.Debug($"ScenarioBuilder client connected: {Context.ConnectionId} for scenario {scenarioId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var scenarioId = Context.GetHttpContext()?.Request.Query["scenarioId"].ToString() ?? "all";
        _connections.Remove(scenarioId, Context.ConnectionId);
        _log.Debug($"ScenarioBuilder client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    public async Task UpdateScenario(int scenarioId, UpdateScenarioDto dto)
    {
        try
        {
            await _scenarioService.UpdateAsync(scenarioId, dto, CancellationToken.None);
            await Clients.Caller.SendAsync("ScenarioSaved", new { scenarioId, timestamp = DateTime.UtcNow });

            // Notify other clients viewing the same scenario
            foreach (var connectionId in _connections.GetConnections(scenarioId.ToString()))
            {
                if (connectionId != Context.ConnectionId)
                {
                    await Clients.Client(connectionId).SendAsync("ScenarioUpdated", new { scenarioId, timestamp = DateTime.UtcNow });
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error updating scenario {scenarioId} via hub");
            await Clients.Caller.SendAsync("ScenarioSaveError", new { scenarioId, error = ex.Message });
        }
    }

    public static ConnectionMapping<string> GetConnections()
    {
        return _connections;
    }
}
