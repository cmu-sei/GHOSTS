// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Proposals;

/// <summary>Only the owner, acting as a person, decides a pending proposal.</summary>
internal static class ProposalRules
{
    public static async Task<(Proposal? Proposal, IResult? Error)> PendingForOwner(
        BuilderDb db, ScenarioState state, Caller caller, int proposalId, CancellationToken ct)
    {
        if (caller.IsAgent)
            return (null, ErrorResults.Forbidden("AGENT_CANNOT_DECIDE",
                "Only the owner can apply or discard a proposal, on its card. Ask them to."));
        if (caller.User != state.Scenario.Owner)
            return (null, ErrorResults.Forbidden("NOT_OWNER",
                $"Only {state.Scenario.Owner}, the scenario's owner, can apply or discard a proposal."));
        var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId && p.ScenarioId == state.Scenario.Id, ct);
        if (proposal is null)
            return (null, ErrorResults.NotFound("PROPOSAL_UNKNOWN", $"There is no proposal {proposalId} on this scenario."));
        if (proposal.Status != ProposalStatus.Pending)
            return (null, ErrorResults.Conflict("NOT_PENDING",
                $"Proposal P-{proposalId} was already {proposal.Status.ToString().ToLowerInvariant()}."));
        return (proposal, null);
    }
}
