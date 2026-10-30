// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Proposals;

/// <summary>POST /scenarios/{id}/proposals/{pid}/discard — the owner declines a proposal; the answer stands.</summary>
public class DiscardEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/proposals/{proposalId:int}/discard",
            async (int id, int proposalId, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var (proposal, error) = await ProposalRules.PendingForOwner(db, state, Caller.From(http), proposalId, ct);
                if (error is not null)
                    return error;

                proposal!.Status = ProposalStatus.Discarded;
                proposal.Decided = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(ProposalView.From(proposal));
            }).WithTags(tag);
}
