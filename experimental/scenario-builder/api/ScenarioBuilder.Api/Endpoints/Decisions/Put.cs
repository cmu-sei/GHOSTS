// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Decisions;

public record PutDecision(JsonNode? Value, Who? Who, string? Reason, string? Quote);

/// <summary>
/// PUT /scenarios/{id}/decisions/{questionId} — record an answer. A person's answer is always stated. The agent may
/// record a stated answer only with the person's own words as its quote, and may never overwrite a stated one.
/// </summary>
public class PutEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/decisions/{questionId}",
            async (int id, string questionId, PutDecision body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                if (state.Kind.Find(questionId) is not var (question, stage))
                    return ErrorResults.NotFound("QUESTION_UNKNOWN", $"There is no question '{questionId}' in this kind.");
                if (stage > state.Scenario.Stage)
                    return ErrorResults.Conflict("STAGE_NOT_OPEN",
                        $"'{questionId}' belongs to stage {stage}, which isn't open yet. Finish stage {state.Scenario.Stage} first.");

                var caller = Caller.From(http);
                var existing = state.Decisions.FirstOrDefault(d => d.QuestionId == questionId);
                var who = caller.IsAgent ? body.Who ?? Who.Proposed : Who.Stated;
                if (caller.IsAgent)
                {
                    if (existing?.Who == Who.Stated)
                        return ErrorResults.Conflict("STATED",
                            $"'{questionId}' was stated by {existing.DecidedBy}. Use propose_change to suggest a different value.");
                    if (who == Who.Stated && string.IsNullOrWhiteSpace(body.Quote))
                        return ErrorResults.BadRequest("QUOTE_REQUIRED",
                            $"Recording '{questionId}' as stated needs the designer's own words as the quote.");
                    if (who == Who.Proposed && string.IsNullOrWhiteSpace(body.Reason))
                        return ErrorResults.BadRequest("REASON_REQUIRED",
                            $"Recording '{questionId}' as proposed needs a one-sentence reason.");
                }

                if (Ledger.ValidateValue(question, body.Value, out var canonical) is { } invalid)
                    return ErrorResults.BadRequest("VALUE_INVALID", invalid);

                var value = canonical!.ToJsonString(Ledger.Storage);
                var now = DateTime.UtcNow;
                db.Edits.Add(new Edit
                {
                    ScenarioId = id, Target = questionId, Before = existing?.Value, After = value,
                    By = caller.User, Via = caller.Via, Time = now
                });
                var decision = existing ?? new Decision { ScenarioId = id, QuestionId = questionId, Stage = stage };
                decision.Value = value;
                decision.Who = who;
                decision.DecidedBy = caller.User;
                decision.Via = caller.Via;
                decision.Reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
                decision.Quote = caller.IsAgent && who == Who.Stated ? body.Quote : null;
                decision.Time = now;
                if (existing is null)
                    db.Decisions.Add(decision);
                state.Scenario.Updated = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(AnsweredView.From(decision));
            }).WithTags(tag);
}
