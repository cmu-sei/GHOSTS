// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Proposals;

public record CreateProposal(string? QuestionId, JsonNode? Value, string? Reason);

/// <summary>POST /scenarios/{id}/proposals — suggest a change to a stated answer. Nothing changes until the owner applies it.</summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/proposals",
            async (int id, CreateProposal body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                if (state.Kind.Find(body.QuestionId ?? string.Empty) is not var (question, _))
                    return ErrorResults.NotFound("QUESTION_UNKNOWN", $"There is no question '{body.QuestionId}' in this kind.");
                var existing = state.Decisions.FirstOrDefault(d => d.QuestionId == question.Id);
                if (existing?.Who != Who.Stated)
                    return ErrorResults.Conflict("NOT_STATED", $"'{question.Id}' isn't stated. Record it with record_answer instead.");
                if (string.IsNullOrWhiteSpace(body.Reason))
                    return ErrorResults.BadRequest("REASON_REQUIRED", $"A proposal to change '{question.Id}' needs a one-sentence reason.");
                if (Ledger.ValidateValue(question, body.Value, out var canonical) is { } invalid)
                    return ErrorResults.BadRequest("VALUE_INVALID", invalid);

                var caller = Caller.From(http);
                var proposal = new Proposal
                {
                    ScenarioId = id,
                    QuestionId = question.Id,
                    Before = existing.Value,
                    After = canonical!.ToJsonString(Ledger.Storage),
                    Reason = body.Reason.Trim(),
                    ProposedBy = caller.User,
                    Via = caller.Via,
                    Status = ProposalStatus.Pending,
                    Created = DateTime.UtcNow
                };
                db.Proposals.Add(proposal);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/scenarios/{id}/proposals/{proposal.Id}", ProposalView.From(proposal));
            }).WithTags(tag);
}
