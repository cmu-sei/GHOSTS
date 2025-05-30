// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Domain.Code;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientSurveyService
{
    Task<bool> ProcessSurveyAsync(HttpContext context, Survey value, CancellationToken ct);
    Task<bool> ProcessEncryptedSurveyAsync(HttpContext context, EncryptedPayload transmission, CancellationToken ct);
}

public class ClientSurveyService(IBackgroundQueue queue) : IClientSurveyService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public Task<bool> ProcessSurveyAsync(HttpContext context, Survey value, CancellationToken ct)
    {
        if (!context.Request.Headers.TryGetValue("ghosts-id", out var id) || string.IsNullOrEmpty(id))
        {
            _log.Warn("Missing ghosts-id header");
            return Task.FromResult(false);
        }

        _log.Info($"Request by {id}");

        var machine = WebRequestReader.GetMachine(context);
        if (!string.IsNullOrEmpty(id))
            machine.Id = new Guid(id);

        if (!machine.IsValid())
        {
            _log.Warn("Invalid machine request");
            return Task.FromResult(false);
        }

        value.MachineId = machine.Id;
        if (value.Created == DateTime.MinValue)
            value.Created = DateTime.UtcNow;

        queue.Enqueue(new QueueEntry { Type = QueueEntry.Types.Survey, Payload = value });
        return Task.FromResult(true);
    }

    public async Task<bool> ProcessEncryptedSurveyAsync(HttpContext context, EncryptedPayload transmission,
        CancellationToken ct)
    {
        if (!context.Request.Headers.TryGetValue("ghosts-name", out var key))
        {
            _log.Warn("Missing ghosts-name header");
            return false;
        }

        try
        {
            var decoded = Base64Encoder.Base64Decode(transmission.Payload);
            var decrypted = Crypto.DecryptStringAes(decoded, key);
            var parsed = JsonConvert.DeserializeObject<Survey>(decrypted);
            return await ProcessSurveyAsync(context, parsed, ct);
        }
        catch (Exception exc)
        {
            _log.Error(exc, "Malformed survey payload");
            return false;
        }
    }
}
