// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientHubService
{
    Task<bool> SendUpdate(Guid machineId, MachineUpdate machineUpdate);
}

public class ClientHubService(IHubContext<ClientHub> hubContext) : IClientHubService
{
    public Task<bool> SendUpdate(Guid machineId, MachineUpdate machineUpdate)
    {
        var connId = ClientHub.GetConnectionId(machineId);
        return connId != null
            ? hubContext.Clients.Client(connId)
                .SendAsync("ReceiveUpdate", JsonSerializer.Serialize(machineUpdate))
                .ContinueWith(_ => true)
            : Task.FromResult(false);
    }
}
