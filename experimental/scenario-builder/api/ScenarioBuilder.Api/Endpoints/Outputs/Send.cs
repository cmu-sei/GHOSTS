// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Outputs;

/// <summary>
/// POST /scenarios/{id}/send/{target} — hands the scenario to a plugin's app. A plugin must be connected first;
/// sending itself isn't built for any app yet, so a connected one says so, and building it changes only this endpoint.
/// </summary>
public class SendEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/send/{target}", async (int id, string target, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is null)
                    return ScenarioState.Missing(id);
                if (Domain.Plugins.Find(target) is not { } plugin)
                    return ErrorResults.NotFound("TARGET_UNKNOWN", $"There is no target '{target}'. Targets: {string.Join(", ", Domain.Plugins.All.Select(p => p.Id))}.");
                var connection = await db.PluginConnections.FirstOrDefaultAsync(c => c.Plugin == target, ct);
                return connection is null
                    ? ErrorResults.Conflict("NOT_CONNECTED", $"Connect {plugin.Name} first: an admin adds its API address with + on its card.")
                    : ErrorResults.NotImplemented("SEND_NOT_BUILT", $"{plugin.Name} is connected at {connection.Url}, but sending a scenario to it isn't built yet.");
            })
            .WithTags(tag);
}
