// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
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
    IClientTimelineService clientTimelineService,
    IClientUpdateService clientUpdateService) : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly CancellationToken _ct = CancellationToken.None;

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await SendId(null);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var m = GetMachine();
        _log.Trace($"{m.Name} {m.Id} ({Context.ConnectionId}) - Disconnecting...");
        await base.OnDisconnectedAsync(exception);
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

    public async Task SendUpdates(string message)
    {
        var context = Context.GetHttpContext();
        var (success, update, _, error) = await clientUpdateService.GetUpdateAsync(context, _ct);

        if (!success)
        {
            await Clients.Caller.SendAsync("ReceiveUpdates", error, _ct);
            return;
        }

        var payload = JsonSerializer.Serialize(update);
        _log.Trace($"SendUpdates to {Context.ConnectionId}: {payload}");
        await Clients.Caller.SendAsync("ReceiveUpdates", payload, _ct);
    }

    public async Task SendTimeline(string message)
    {
        var context = Context.GetHttpContext();
        var (success, result, error) = await clientTimelineService.ProcessTimelineAsync(context, message, _ct);

        await Clients.Caller.SendAsync("ReceiveTimeline", success ? result : error, _ct);
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
