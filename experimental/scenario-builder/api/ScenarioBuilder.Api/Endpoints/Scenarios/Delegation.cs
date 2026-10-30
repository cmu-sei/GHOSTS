// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

public record PutDelegation(bool On);

public record DelegationView(bool Delegated);

/// <summary>
/// PUT /scenarios/{id}/delegation — the owner hands the agent every remaining decision, gates included, or takes
/// them back. A person's call only: the agent can't grant itself this, whatever the model says.
/// </summary>
public class DelegationEndpoint : IEndpoint
{
    /// <summary>The log target that records a handover.</summary>
    public const string Target = "delegation";

    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/delegation",
            async (int id, PutDelegation body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (caller.IsAgent)
                    return ErrorResults.Forbidden("AGENT_CANNOT_DECIDE", "Only the owner can hand over the decisions.");
                if (caller.User != state.Scenario.Owner)
                    return ErrorResults.Forbidden("NOT_OWNER", $"Only {state.Scenario.Owner}, the scenario's owner, can hand over the decisions.");

                if (state.Scenario.Delegated != body.On)
                {
                    var now = DateTime.UtcNow;
                    db.Edits.Add(new Edit
                    {
                        ScenarioId = id, Target = Target,
                        Before = JsonValue.Create(state.Scenario.Delegated ? "on" : "off").ToJsonString(),
                        After = JsonValue.Create(body.On ? "on" : "off").ToJsonString(),
                        By = caller.User, Via = caller.Via, Time = now
                    });
                    state.Scenario.Delegated = body.On;
                    state.Scenario.Updated = now;
                    await db.SaveChangesAsync(ct);
                }
                return Results.Ok(new DelegationView(state.Scenario.Delegated));
            }).WithTags(tag);
}
