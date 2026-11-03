// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Proposals;

/// <summary>GET /scenarios/{id}/proposals[?status=pending] — the scenario's proposals, oldest first.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/proposals", async (int id, string? status, BuilderDb db, CancellationToken ct) =>
        {
            if (!await db.Scenarios.AnyAsync(s => s.Id == id, ct))
                return ScenarioState.Missing(id);
            ProposalStatus? wanted = null;
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<ProposalStatus>(status, ignoreCase: true, out var parsed))
                    return ErrorResults.BadRequest("STATUS_UNKNOWN", "Status is one of: pending, applied, discarded.");
                wanted = parsed;
            }
            var proposals = await db.Proposals
                .Where(p => p.ScenarioId == id && (wanted == null || p.Status == wanted))
                .OrderBy(p => p.Id)
                .ToListAsync(ct);
            return Results.Ok(proposals.Select(ProposalView.From));
        }).WithTags(tag);
}
