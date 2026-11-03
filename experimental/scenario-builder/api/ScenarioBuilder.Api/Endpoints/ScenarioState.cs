// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints;

/// <summary>A scenario with its scenario type and the rows the ledger rules read, loaded together.</summary>
public sealed record ScenarioState(Scenario Scenario, ScenarioType ScenarioType, List<Decision> Decisions, List<Approval> Approvals)
{
    public static async Task<ScenarioState?> Load(BuilderDb db, ScenarioTypeCatalog scenarioTypes, int id, CancellationToken ct)
    {
        var scenario = await db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (scenario is null)
            return null;
        var scenarioType = scenarioTypes.Find(scenario.ScenarioTypeId, scenario.ScenarioTypeVersion)
                   ?? throw new InvalidOperationException($"Scenario {id} is on scenario type '{scenario.ScenarioTypeId}' {scenario.ScenarioTypeVersion}, which isn't published here.");
        var decisions = await db.Decisions.Where(d => d.ScenarioId == id).ToListAsync(ct);
        var approvals = await db.Approvals.Where(a => a.ScenarioId == id).OrderBy(a => a.Id).ToListAsync(ct);
        return new ScenarioState(scenario, scenarioType, decisions, approvals);
    }

    public static IResult Missing(int id) => ErrorResults.NotFound("SCENARIO_UNKNOWN", $"There is no scenario {id}.");

    public ProgressView Progress() => Ledger.Progress(ScenarioType, Scenario, Decisions, Approvals);
}
