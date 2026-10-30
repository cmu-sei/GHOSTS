// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Proposals;

/// <summary>
/// POST /scenarios/{id}/proposals/{pid}/apply — the owner accepts a proposal: its value becomes the stated answer.
/// Refused when the answer changed after the proposal was made, so an old proposal never overwrites a newer answer.
/// </summary>
public class ApplyEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/proposals/{proposalId:int}/apply",
            async (int id, int proposalId, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                var (proposal, error) = await ProposalRules.PendingForOwner(db, state, caller, proposalId, ct);
                if (error is not null)
                    return error;

                var decision = state.Decisions.Single(d => d.QuestionId == proposal!.QuestionId);
                if (decision.Value != proposal!.Before)
                    return ErrorResults.Conflict("STALE_PROPOSAL",
                        $"'{proposal.QuestionId}' has changed since P-{proposal.Id} was proposed. Discard it, and propose again if it still applies.");

                var now = DateTime.UtcNow;
                db.Edits.Add(new Edit
                {
                    ScenarioId = id, Target = decision.QuestionId, Before = decision.Value, After = proposal.After,
                    By = caller.User, Via = Via.Person, Time = now
                });
                decision.Value = proposal.After;
                decision.Who = Who.Stated;
                decision.DecidedBy = caller.User;
                decision.Via = Via.Person;
                decision.Reason = proposal.Reason;
                decision.Quote = null;
                decision.Time = now;
                proposal.Status = ProposalStatus.Applied;
                proposal.Decided = now;
                state.Scenario.Updated = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(ProposalView.From(proposal));
            }).WithTags(tag);
}
