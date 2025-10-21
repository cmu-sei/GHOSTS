// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Hubs;

public class ClientHub(
    IClientIdService clientIdService,
    IClientResultsService clientResultsService,
    IClientSurveyService clientSurveyService,
    IClientTimelineService clientTimelineService) : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly CancellationToken _ct = CancellationToken.None;
    private static readonly ConcurrentDictionary<Guid, string> _machineConnectionMap = new();

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await SendId(null);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var connectionId = Context.ConnectionId;

        var machineId = _machineConnectionMap.FirstOrDefault(x => x.Value == connectionId).Key;
        if (machineId != Guid.Empty)
        {
            _machineConnectionMap.TryRemove(machineId, out _);
        }

        var m = GetMachine();
        _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - Disconnecting...");
        await base.OnDisconnectedAsync(exception);
    }

    public static string GetConnectionId(Guid machineId)
    {
        return _machineConnectionMap.TryGetValue(machineId, out var connId) ? connId : null;
    }

    public async Task SendId(string id)
    {
        var context = Context.GetHttpContext();
        var (success, machineId, error) = await clientIdService.GetMachineIdAsync(context, _ct);
        if (!success)
        {
            _log.Error($"SendId failed: {error}");
            await Clients.Caller.SendAsync("ReceiveId", null, _ct);
            return;
        }

        _machineConnectionMap[machineId] = Context.ConnectionId;

        _log.Trace($"{machineId} ({Context.ConnectionId}) - ReceiveId");
        await Clients.Caller.SendAsync("ReceiveId", machineId.ToString(), _ct);
    }

    public async Task SendResults(TransferLogDump message)
    {
        var context = Context.GetHttpContext();
        var success = await clientResultsService.ProcessResultAsync(context, message, _ct);

        await Clients.Caller.SendAsync("ReceiveResultsAck", success ? "Results received" : "Rejected", _ct);
    }

    public async Task SendSurvey(Survey message)
    {
        var context = Context.GetHttpContext();
        var success = await clientSurveyService.ProcessSurveyAsync(context, message, _ct);

        await Clients.Caller.SendAsync("ReceiveSurveyAck", success ? "Survey received" : "Rejected", _ct);
    }

    public async Task SendUpdate(MachineUpdate machineUpdate)
    {
        var context = Context.GetHttpContext();
        var (success, result, error) = await clientTimelineService.ProcessTimelineAsync(context, JsonSerializer.Serialize(machineUpdate.Update), _ct);

        await Clients.Caller.SendAsync("ReceiveUpdate", success ? result : error, _ct);
    }

    public async Task SendHeartbeat(string message)
    {
        var m = GetMachine();

        _log.Trace(m.Id != Guid.Empty
            ? $"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveHeartbeat"
            : $"New machine — ({Context.ConnectionId}) - ReceiveHeartbeat");

        await Clients.Caller.SendAsync("ReceiveHeartbeat", DateTime.UtcNow, _ct);
    }

    public async Task SendMessage(string message)
    {
        var m = GetMachine();
        _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveMessage");
        await Clients.All.SendAsync("ReceiveMessage", $"{message} {DateTime.UtcNow}", _ct);
    }

    public async Task SendSpecificMessage(string message)
    {
        var m = GetMachine();
        _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - ReceiveSpecificMessage");
        await Clients.Caller.SendAsync("ReceiveSpecificMessage", message, _ct);
    }

    private Machine GetMachine()
    {
        return WebRequestReader.GetMachine(Context.GetHttpContext());
    }
}
