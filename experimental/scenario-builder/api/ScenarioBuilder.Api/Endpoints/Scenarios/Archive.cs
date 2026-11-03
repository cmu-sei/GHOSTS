// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

public record PutArchive(bool Archived);

/// <summary>PUT /scenarios/{id}/archive — the owner hides a scenario from the list, or brings it back. Nothing else changes.</summary>
public class ArchiveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/archive", async (int id, PutArchive body, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is not { } scenario)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (caller.IsAgent)
                    return ErrorResults.Forbidden("AGENT_CANNOT_DECIDE", "Only the owner archives a scenario.");
                if (caller.User != scenario.Owner)
                    return ErrorResults.Forbidden("NOT_OWNER", $"Only {scenario.Owner}, the scenario's owner, can archive it.");
                scenario.Archived = body.Archived;
                await db.SaveChangesAsync(ct);
                return Results.Ok(ScenarioSummary.From(scenario));
            })
            .WithTags(tag);
}
