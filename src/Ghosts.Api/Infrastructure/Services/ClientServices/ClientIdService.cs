// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NLog;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientIdService
{
    Task<(bool Success, Guid MachineId, string Error)> GetMachineIdAsync(HttpContext context, CancellationToken ct);
}

public class ClientIdService(IMachineService machineService) : IClientIdService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task<(bool Success, Guid MachineId, string Error)> GetMachineIdAsync(HttpContext context,
        CancellationToken ct)
    {
        try
        {
            var idHeader = context.Request.Headers.TryGetValue("ghosts-id", out var id) ? id.ToString() : string.Empty;

            _log.Info($"Request by {idHeader}");

            var findMachineResponse = await machineService.FindOrCreate(context, ct);
            if (!findMachineResponse.IsValid())
            {
                _log.Error($"FindOrCreate failed for {idHeader}: {findMachineResponse.Error}");
                return (false, Guid.Empty, findMachineResponse.Error);
            }

            return (true, findMachineResponse.Machine.Id, string.Empty);
        }
        catch (Exception e)
        {
            _log.Error(e);
            return (false, Guid.Empty, $"Exception occured: {e.Message}");
        }
    }
}
