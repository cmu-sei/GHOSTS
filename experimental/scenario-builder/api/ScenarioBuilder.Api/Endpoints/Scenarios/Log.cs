// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>One thing that happened. Type is answer, draft, proposal, approval or delegation.</summary>
public record LogEntry(DateTime Time, string Type, string Target, JsonNode? Before, JsonNode? After, string By, Via Via, string? Reason);

/// <summary>GET /scenarios/{id}/log — every answer, draft, proposal and approval, in time order.</summary>
public class LogEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/log", async (int id, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
        {
            var state = await ScenarioState.Load(db, kinds, id, ct);
            if (state is null)
                return ScenarioState.Missing(id);
            var draftFields = state.Kind.Drafts.Select(d => d.Field).ToHashSet();

            var edits = await db.Edits.Where(e => e.ScenarioId == id).OrderBy(e => e.Id).ToListAsync(ct);
            var proposals = await db.Proposals.Where(p => p.ScenarioId == id).OrderBy(p => p.Id).ToListAsync(ct);
            var entries = edits
                .Select(e => new LogEntry(e.Time, e.Target == DelegationEndpoint.Target ? "delegation" : draftFields.Contains(e.Target) ? "draft" : "answer", e.Target,
                    Parse(e.Before), Parse(e.After), e.By, e.Via, null))
                .Concat(proposals.Select(p => new LogEntry(p.Created, "proposal", p.QuestionId,
                    Parse(p.Before), Parse(p.After), p.ProposedBy, p.Via, p.Reason)))
                .Concat(state.Approvals.Select(a => new LogEntry(a.Time, "approval", $"stage {a.Stage}",
                    null, null, a.ApprovedBy, a.Via, null)))
                .OrderBy(e => e.Time);
            return Results.Ok(entries);
        }).WithTags(tag);

    private static JsonNode? Parse(string? json) => json is null ? null : JsonNode.Parse(json);
}
