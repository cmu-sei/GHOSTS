// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientResultsService
{
    Task<bool> ProcessResultAsync(HttpContext context, TransferLogDump logDump, CancellationToken ct);
    Task<bool> ProcessEncryptedAsync(HttpContext context, EncryptedPayload encrypted, CancellationToken ct);
}

public class ClientResultsService(IBackgroundQueue queue) : IClientResultsService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public Task<bool> ProcessResultAsync(HttpContext context, TransferLogDump logDump, CancellationToken ct)
    {
        try
        {
            if (logDump == null || string.IsNullOrWhiteSpace(logDump.Log))
                return Task.FromResult(false);

            var machine = WebRequestReader.GetMachine(context);

            if (!context.Request.Headers.TryGetValue("ghosts-id", out var id) || string.IsNullOrEmpty(id))
            {
                if (!machine.IsValid())
                    return Task.FromResult(false);
            }
            else
            {
                machine.Id = new Guid(id);
            }

            _log.Info($"Payload received from {machine.Id}: {logDump.Log}");

            queue.Enqueue(new QueueEntry
            {
                Payload = new MachineQueueEntry
                {
                    Machine = machine,
                    LogDump = logDump,
                    HistoryType = Machine.MachineHistoryItem.HistoryType.PostedResults
                },
                Type = QueueEntry.Types.Machine
            });

            return Task.FromResult(true);
        }
        catch (Exception e)
        {
            _log.Error(e);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> ProcessEncryptedAsync(HttpContext context, EncryptedPayload encrypted, CancellationToken ct)
    {
        if (!context.Request.Headers.TryGetValue("ghosts-name", out var key))
            return false;

        try
        {
            var payload = Base64Encoder.Base64Decode(encrypted.Payload);
            var decrypted = Crypto.DecryptStringAes(payload, key);
            var parsed = JsonConvert.DeserializeObject<TransferLogDump>(decrypted);

            return await ProcessResultAsync(context, parsed, ct);
        }
        catch (Exception e)
        {
            _log.Error(e, "Decryption or deserialization failed");
            return false;
        }
    }
}
