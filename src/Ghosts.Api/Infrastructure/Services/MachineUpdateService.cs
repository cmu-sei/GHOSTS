// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

public interface IMachineUpdateService
{
    Task<MachineUpdate> GetAsync(Guid id, string currentUsername, CancellationToken ct);

    Task<MachineUpdate> CreateAsync(MachineUpdate model, CancellationToken ct);
    Task<int> MarkAsDeletedAsync(int id, Guid machineId, CancellationToken ct);

    Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdate, CancellationToken ct);

    Task<MachineUpdate> GetById(int updateId, CancellationToken ct);

    Task<IEnumerable<MachineUpdate>> GetByMachineId(Guid machineId, CancellationToken ct);

    Task<IEnumerable<MachineUpdate>> GetByStatus(StatusType status, CancellationToken ct);

    Task<IEnumerable<MachineUpdate>> GetByType(UpdateClientConfig.UpdateType type, CancellationToken ct);

    Task<MachineUpdate> CreateByActionRequest(NpcRecord npc, AiModels.ActionRequest actionRequest,
        CancellationToken ct);
}

public class MachineUpdateService(
    ApplicationDbContext context,
    IServiceProvider serviceProvider,
    IClientHubService clientHubService) : IMachineUpdateService {
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdateViewModel,
        CancellationToken ct)
    {
        var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

        var group = context.Groups.Include(o => o.GroupMachines).FirstOrDefault(x => x.Id == groupId);

        if (group == null)
            return;

        foreach (var machineMapping in group.GroupMachines)
        {
            machineUpdate.MachineId = machineMapping.MachineId;
            context.MachineUpdates.Add(machineUpdate);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<MachineUpdate> GetAsync(Guid machineId, string currentUsername, CancellationToken ct)
    {
        if (machineId == Guid.Empty && string.IsNullOrEmpty(currentUsername))
            return new MachineUpdate();

        // Build the base query with conditions that are always true
        var query = context.MachineUpdates
            .Where(m => m.ActiveUtc <= DateTime.UtcNow && m.Status == StatusType.Active);

        // Adjust the query based on the provided arguments
        if (machineId != Guid.Empty)
        {
            query = query.Where(m => m.MachineId == machineId);
        }
        else
        {
            if (!string.IsNullOrEmpty(currentUsername))
            {
                var usernameLower = currentUsername.ToLower();
                query = query.Where(m => m.Username.ToLower().StartsWith(usernameLower));
            }
        }

        var update = await query.FirstOrDefaultAsync(ct);
        return update;
    }

    public async Task<MachineUpdate> GetById(int updateId, CancellationToken ct)
    {
        return await context.MachineUpdates.FirstOrDefaultAsync(x => x.Id == updateId, ct);
    }

    public async Task<IEnumerable<MachineUpdate>> GetByMachineId(Guid machineId, CancellationToken ct)
    {
        return await context.MachineUpdates.Where(x => x.MachineId == machineId).ToListAsync(ct);
    }

    public async Task<IEnumerable<MachineUpdate>> GetByType(UpdateClientConfig.UpdateType type,
        CancellationToken ct)
    {
        return await context.MachineUpdates.Where(x => x.Type == type).ToListAsync(ct);
    }

    public async Task<IEnumerable<MachineUpdate>> GetByStatus(StatusType status, CancellationToken ct)
    {
        return await context.MachineUpdates.Where(x => x.Status == status).ToListAsync(ct);
    }

    public async Task<MachineUpdate> CreateAsync(MachineUpdate model, CancellationToken ct)
    {
        var machineUpdate = await GetById(model.Id, ct);
        if (machineUpdate != null)
            return machineUpdate;

        model.Update.Id = Guid.NewGuid();

        context.MachineUpdates.Add(model);
        await context.SaveChangesAsync(ct);
        return model;
    }

    public async Task<int> MarkAsDeletedAsync(int id, Guid machineId, CancellationToken ct)
    {
        var model = await context.MachineUpdates.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (model == null)
        {
            _log.Error($"Machine update not found for id: {id}");
            throw new InvalidOperationException("Machine Update not found");
        }

        model.Status = StatusType.Deleted;
        model.MachineId = machineId;
        _log.Info($"Marking machine update {id} as deleted.");

        var operation = await context.SaveChangesAsync(ct);
        if (operation >= 1)
        {
            _log.Info($"Machine update {id} marked as deleted successfully.");
            return id;
        }

        _log.Error($"Could not mark machine update {id} as deleted: {operation}");
        throw new InvalidOperationException("Could not delete Machine Update");
    }

    public async Task<MachineUpdate> CreateByActionRequest(NpcRecord npc, AiModels.ActionRequest actionRequest,
        CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        actionRequest.Action = ParseAction(actionRequest.Action);

        if (npc.MachineId.HasValue && actionRequest.Handler.TryParseHandlerType(out var handler))
        {
            var timelineEvent = new TimelineEvent { Command = "browse" };
            timelineEvent.CommandArgs.Add(actionRequest.Action);

            var timelineHandler = new TimelineHandler { HandlerType = handler };
            timelineHandler.Initial = actionRequest.Action;
            timelineHandler.TimeLineEvents.Add(timelineEvent);

            var handlers = new List<TimelineHandler> { timelineHandler };

            var timeline = new Timeline
            {
                Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run, TimeLineHandlers = handlers
            };

            var o = new MachineUpdate
            {
                Status = StatusType.Active,
                Update = timeline,
                ActiveUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                MachineId = npc.MachineId.Value,
                Type = UpdateClientConfig.UpdateType.TimelinePartial
            };

            // Attempt real-time delivery; fallback to DB-delivery if no active socket
            if (!await clientHubService.SendUpdate(npc.MachineId.Value, o))
            {
                context.MachineUpdates.Add(o);
                await context.SaveChangesAsync(ct);
            }

            return o;
        }

        return null;
    }

    private static string ParseAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return string.Empty;

        // Extract the URL using regex
        var match = System.Text.RegularExpressions.Regex.Match(action, @"(https?:\/\/)?([\w\-]+\.)+[\w]{2,}(\/\S*)?");
        if (!match.Success)
            return action;

        var url = match.Value;

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

}
