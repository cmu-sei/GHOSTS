// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Gates;

public record ApprovalView(int Stage, string Hash);

/// <summary>
/// POST /scenarios/{id}/gates/{stage}/approve — the owner, as a person, approves the open stage once nothing but the
/// approval is left; while the owner has delegated the scenario, the agent may too, and the approval says so.
/// The approval records a hash of what it covered, so a later change voids it.
/// </summary>
public class ApproveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/gates/{stage:int}/approve",
            async (int id, int stage, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (caller.IsAgent && !state.Scenario.Delegated)
                    return ErrorResults.Forbidden("AGENT_CANNOT_DECIDE",
                        "Only the owner can approve, on the Questions page, unless the owner hands you the decisions. Ask them to approve.");
                if (caller.User != state.Scenario.Owner)
                    return ErrorResults.Forbidden("NOT_OWNER", $"Only {state.Scenario.Owner}, the scenario's owner, can approve.");
                if (stage < 0 || stage > state.Kind.LastStage)
                    return ErrorResults.NotFound("STAGE_UNKNOWN", $"This kind has stages 0 to {state.Kind.LastStage}.");
                if (stage > state.Scenario.Stage)
                    return ErrorResults.Conflict("STAGE_NOT_OPEN", $"Stage {stage} isn't open yet; stage {state.Scenario.Stage} is.");
                if (state.Kind.Stages[stage].Gate.Approval is null)
                    return ErrorResults.Conflict("NO_APPROVAL_NEEDED", $"Stage {stage} needs no approval. Advance once its questions are answered.");
                // A closed stage is complete by definition; approving it again only renews what changed after its approval.
                var blocking = state.Progress().Gate.Findings.Where(f => f.Stage == stage && !f.Code.StartsWith("APPROVAL_")).ToList();
                if (blocking.Count > 0)
                    return ErrorResults.Conflict("GATE_NOT_READY",
                        $"Stage {stage} isn't ready to approve: {string.Join(" ", blocking.Select(f => f.Message))}");

                var hash = Ledger.StageHash(state.Kind, stage, state.Decisions, Ledger.ReadDrafts(state.Scenario));
                var now = DateTime.UtcNow;
                db.Approvals.Add(new Approval { ScenarioId = id, Stage = stage, ApprovedBy = caller.User, Via = caller.Via, Hash = hash, Time = now });
                state.Scenario.Updated = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ApprovalView(stage, hash));
            }).WithTags(tag);
}
