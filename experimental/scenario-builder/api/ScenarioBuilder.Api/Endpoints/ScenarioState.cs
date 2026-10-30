// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints;

/// <summary>A scenario with its kind and the rows the ledger rules read, loaded together.</summary>
public sealed record ScenarioState(Scenario Scenario, Kind Kind, List<Decision> Decisions, List<Approval> Approvals)
{
    public static async Task<ScenarioState?> Load(BuilderDb db, KindCatalog kinds, int id, CancellationToken ct)
    {
        var scenario = await db.Scenarios.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (scenario is null)
            return null;
        var kind = kinds.Find(scenario.KindId)
                   ?? throw new InvalidOperationException($"Scenario {id} uses kind '{scenario.KindId}', which isn't loaded.");
        var decisions = await db.Decisions.Where(d => d.ScenarioId == id).ToListAsync(ct);
        var approvals = await db.Approvals.Where(a => a.ScenarioId == id).OrderBy(a => a.Id).ToListAsync(ct);
        return new ScenarioState(scenario, kind, decisions, approvals);
    }

    public static IResult Missing(int id) => ErrorResults.NotFound("SCENARIO_UNKNOWN", $"There is no scenario {id}.");

    public ProgressView Progress() => Ledger.Progress(Kind, Scenario, Decisions, Approvals);
}
