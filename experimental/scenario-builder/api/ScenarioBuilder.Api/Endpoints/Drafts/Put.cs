// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Drafts;

public record PutDraft(string? Text);

public record DraftView(string Field, string Text);

/// <summary>
/// PUT /scenarios/{id}/drafts/{field} — write a drafted field, by the agent or the person. It is craft, so the agent
/// writes it directly; the change is logged, and it voids any approval that covered the old text.
/// </summary>
public class PutEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/drafts/{field}",
            async (int id, string field, PutDraft body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var draft = state.Kind.Drafts.FirstOrDefault(d => d.Field == field);
                if (draft is null)
                    return ErrorResults.NotFound("DRAFT_UNKNOWN",
                        $"There is no draft '{field}' in this kind. Drafts: {string.Join(", ", state.Kind.Drafts.Select(d => d.Field))}.");
                if (string.IsNullOrWhiteSpace(body.Text))
                    return ErrorResults.BadRequest("TEXT_REQUIRED", $"The {field} draft needs text.");
                if (state.Scenario.Stage < draft.DueAfterStage || !Ledger.StageAnswered(state.Kind, draft.DueAfterStage, state.Decisions))
                    return ErrorResults.Conflict("DRAFT_NOT_DUE",
                        $"The {field} draft is due once stage {draft.DueAfterStage}'s questions are answered.");

                var caller = Caller.From(http);
                var drafts = Ledger.ReadDrafts(state.Scenario);
                var now = DateTime.UtcNow;
                db.Edits.Add(new Edit
                {
                    ScenarioId = id, Target = field, Before = drafts[field]?.ToJsonString(Ledger.Storage),
                    After = JsonValue.Create(body.Text).ToJsonString(Ledger.Storage), By = caller.User, Via = caller.Via, Time = now
                });
                drafts[field] = body.Text;
                state.Scenario.Drafts = drafts.ToJsonString(Ledger.Storage);
                state.Scenario.Updated = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new DraftView(field, body.Text));
            }).WithTags(tag);
}
