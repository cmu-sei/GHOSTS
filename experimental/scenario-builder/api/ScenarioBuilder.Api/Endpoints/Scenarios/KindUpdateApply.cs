// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>The version the owner reviewed, so a newer publish since then isn't applied unseen.</summary>
public record ApplyKindUpdate(string? Version);

/// <summary>
/// POST /scenarios/{id}/kind-update — moves the scenario to the latest published version of its kind, as the preview
/// says: answers carry over, dropped ones are logged with their old value, approvals of stages that no longer exist
/// go, and pending proposals on dropped questions are discarded. The owner's call, as a person.
/// </summary>
public class KindUpdateApplyEndpoint : IEndpoint
{
    /// <summary>The log target that records an update.</summary>
    public const string Target = "scenario type version";

    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/kind-update",
            async (int id, ApplyKindUpdate body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var state = await ScenarioState.Load(db, kinds, id, ct);
                if (state is null)
                    return ScenarioState.Missing(id);
                var caller = Caller.From(http);
                if (caller.IsAgent)
                    return ErrorResults.Forbidden("AGENT_CANNOT_UPDATE", "Only the owner can update the scenario to a new version of its scenario type.");
                var scenario = state.Scenario;
                if (caller.User != scenario.Owner)
                    return ErrorResults.Forbidden("NOT_OWNER", $"Only {scenario.Owner}, the scenario's owner, can update it to a new version of its scenario type.");
                var latest = kinds.Find(scenario.KindId)!;
                if (latest.Version == scenario.KindVersion)
                    return ErrorResults.Conflict("UP_TO_DATE", $"The scenario is already on the latest version, {latest.Version}.");
                if (body.Version != latest.Version)
                    return ErrorResults.Conflict("VERSION_CHANGED", $"The latest version is {latest.Version}, not {body.Version}. Review the update again.");

                var plan = KindUpdate.Plan(state.Kind, latest, scenario, state.Decisions);
                var now = DateTime.UtcNow;
                void Log(string target, string? before, string? after) =>
                    db.Edits.Add(new Edit { ScenarioId = id, Target = target, Before = before, After = after ?? "null", By = caller.User, Via = caller.Via, Time = now });

                var dropped = plan.Dropped.Select(d => d.Id).ToHashSet();
                foreach (var decision in state.Decisions)
                {
                    if (dropped.Contains(decision.QuestionId))
                    {
                        Log(decision.QuestionId, decision.Value, null);
                        db.Decisions.Remove(decision);
                    }
                    else
                        decision.Stage = plan.Carried.Single(c => c.Id == decision.QuestionId).ToStage;
                }
                var stillAsked = latest.AllQuestions.Select(q => q.Id).ToHashSet();
                foreach (var proposal in await db.Proposals.Where(p => p.ScenarioId == id && p.Status == ProposalStatus.Pending).ToListAsync(ct))
                    if (!stillAsked.Contains(proposal.QuestionId) || dropped.Contains(proposal.QuestionId))
                    {
                        proposal.Status = ProposalStatus.Discarded;
                        proposal.Decided = now;
                    }
                db.Approvals.RemoveRange(state.Approvals.Where(a => a.Stage >= latest.Stages.Count));
                var drafts = Ledger.ReadDrafts(scenario);
                foreach (var field in plan.DraftsDropped)
                {
                    Log(field, drafts[field]?.ToJsonString(Ledger.Storage), null);
                    drafts.Remove(field);
                }
                scenario.Drafts = drafts.ToJsonString(Ledger.Storage);
                Log(Target, JsonValue.Create(scenario.KindVersion).ToJsonString(), JsonValue.Create(latest.Version).ToJsonString());
                scenario.KindVersion = latest.Version;
                scenario.Stage = plan.StageTo;
                scenario.Updated = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(plan);
            }).WithTags(tag);
}
