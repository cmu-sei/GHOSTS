// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>One thing that happened. Type is answer, draft, proposal, delegation or update (to a new version of the scenario type).</summary>
public record LogEntry(DateTime Time, string Type, string Target, JsonNode? Before, JsonNode? After, string By, Via Via, string? Reason);

/// <summary>GET /scenarios/{id}/log — every answer, draft, proposal, delegation and update, in time order.</summary>
public class LogEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/log", async (int id, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
        {
            var state = await ScenarioState.Load(db, scenarioTypes, id, ct);
            if (state is null)
                return ScenarioState.Missing(id);
            var draftFields = state.ScenarioType.Drafts.Select(d => d.Field).ToHashSet();

            var edits = await db.Edits.Where(e => e.ScenarioId == id).OrderBy(e => e.Id).ToListAsync(ct);
            var proposals = await db.Proposals.Where(p => p.ScenarioId == id).OrderBy(p => p.Id).ToListAsync(ct);
            var entries = edits
                .Select(e => new LogEntry(e.Time, e.Target == DelegationEndpoint.Target ? "delegation" : e.Target == ScenarioTypeUpdateApplyEndpoint.Target ? "update" : draftFields.Contains(e.Target) ? "draft" : "answer", e.Target,
                    Parse(e.Before), Parse(e.After), e.By, e.Via, null))
                .Concat(proposals.Select(p => new LogEntry(p.Created, "proposal", p.QuestionId,
                    Parse(p.Before), Parse(p.After), p.ProposedBy, p.Via, p.Reason)))
                .OrderBy(e => e.Time);
            return Results.Ok(entries);
        }).WithTags(tag);

    private static JsonNode? Parse(string? json) => json is null ? null : JsonNode.Parse(json);
}
