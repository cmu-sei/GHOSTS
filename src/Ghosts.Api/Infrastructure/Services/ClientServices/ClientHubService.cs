// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientHubService
{
    Task<bool> SendUpdate(Guid machineId, MachineUpdate machineUpdate);
}

public class ClientHubService(IHubContext<ClientHub> hubContext) : IClientHubService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    public Task<bool> SendUpdate(Guid machineId, MachineUpdate machineUpdate)
    {
        try
        {
            var connId = ClientHub.GetConnectionId(machineId);
            return connId != null
                ? hubContext.Clients.Client(connId)
                    .SendAsync("ReceiveUpdate", JsonSerializer.Serialize(machineUpdate))
                    .ContinueWith(_ => true)
                : Task.FromResult(false);
        }
        catch (Exception e)
        {
            _log.Error(e);
            return Task.FromResult(false);
        }
    }
}
