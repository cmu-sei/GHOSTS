// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

public record CreateScenario(string? Name, string? KindId);

public record ScenarioSummary(
    int Id, string Slug, string Name, string KindId, string KindVersion, string Owner, int Stage, DateTime Created, DateTime Updated, bool Archived)
{
    public static ScenarioSummary From(Scenario s) =>
        new(s.Id, s.Slug, s.Name, s.KindId, s.KindVersion, s.Owner, s.Stage, s.Created, s.Updated, s.Archived);
}

/// <summary>POST /scenarios — a new scenario on a kind's first stage. The caller becomes its owner.</summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios", async (CreateScenario body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
        {
            var name = body.Name?.Trim();
            if (string.IsNullOrEmpty(name))
                return ErrorResults.BadRequest("NAME_REQUIRED", "Give the scenario a name.");
            var kind = kinds.Find(body.KindId ?? string.Empty);
            if (kind is null)
                return ErrorResults.BadRequest("KIND_UNKNOWN",
                    $"There is no kind '{body.KindId}'. Kinds: {string.Join(", ", kinds.All.Select(k => k.Id))}.");

            var taken = (await db.Scenarios.Select(s => s.Slug).ToListAsync(ct)).ToHashSet();
            var caller = Caller.From(http);
            var now = DateTime.UtcNow;
            var scenario = new Scenario
            {
                Slug = Slug.Unique(name, taken),
                Name = name,
                KindId = kind.Id,
                KindVersion = kind.Version,
                Owner = caller.User,
                Created = now,
                Updated = now
            };
            db.Scenarios.Add(scenario);
            await db.SaveChangesAsync(ct);

            // A question that defaults to the scenario's name starts answered with it; the owner can change it later.
            var value = JsonValue.Create(name).ToJsonString(Ledger.Storage);
            foreach (var stage in kind.Stages)
                foreach (var q in stage.Questions.Where(q => q.Default == Kind.ScenarioNameDefault))
                {
                    db.Edits.Add(new Edit { ScenarioId = scenario.Id, Target = q.Id, After = value, By = caller.User, Via = caller.Via, Time = now });
                    db.Decisions.Add(new Decision
                    {
                        ScenarioId = scenario.Id, QuestionId = q.Id, Stage = stage.Number, Value = value,
                        Who = caller.IsAgent ? Who.Proposed : Who.Stated, DecidedBy = caller.User, Via = caller.Via,
                        Reason = "Defaults to the scenario's name.", Time = now
                    });
                }
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/scenarios/{scenario.Id}", ScenarioSummary.From(scenario));
        }).WithTags(tag);
}
