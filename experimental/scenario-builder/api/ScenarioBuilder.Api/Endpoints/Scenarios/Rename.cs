// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

public record PutName(string? Name);

/// <summary>PUT /scenarios/{id}/name — the owner renames a scenario. Its slug stays, so exports and consumers keep finding it.</summary>
public class RenameEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/name", async (int id, PutName body, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is not { } scenario)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (caller.IsAgent)
                    return ErrorResults.Forbidden("AGENT_CANNOT_DECIDE", "Only the owner renames a scenario.");
                if (caller.User != scenario.Owner)
                    return ErrorResults.Forbidden("NOT_OWNER", $"Only {scenario.Owner}, the scenario's owner, can rename it.");
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the scenario a name.");
                scenario.Name = name;
                scenario.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(ScenarioSummary.From(scenario));
            })
            .WithTags(tag);
}
