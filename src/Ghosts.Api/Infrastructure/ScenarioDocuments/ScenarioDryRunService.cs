// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

public interface IScenarioDryRunService
{
    Task<IReadOnlyList<ScenarioFinding>> RunAsync(JsonObject document, CancellationToken ct);
}

/// <summary>
/// Tier 4: would this document actually load, and would it put anyone in the exercise? The only way to
/// know is to do it, so the document is mapped, created and its population generated inside a
/// transaction that is rolled back whatever happens. Nothing survives — the scenario and NPC counts are
/// the same before and after — and every exception on the way becomes a finding instead of a stack
/// trace. This is the cheap half of tier 4; tier 5, checking a deployed exercise against its document,
/// is not built.
///
/// It does not compile the scenario. ScenarioCompilerService adapts an environment during a scenario,
/// which is a different question from whether an authored document is ready; the document's population
/// is population.pools, and those are what an execution generates NPCs from.
/// </summary>
public class ScenarioDryRunService(
    ApplicationDbContext context,
    IScenarioService scenarios) : IScenarioDryRunService
{
    private const int Tier = 4;

    public async Task<IReadOnlyList<ScenarioFinding>> RunAsync(JsonObject document, CancellationToken ct)
    {
        var findings = new List<ScenarioFinding>();
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            Scenario scratch;
            try
            {
                scratch = await scenarios.CreateAsync(ScenarioDocumentMapper.FromDocument(document), ct);
            }
            catch (Exception ex)
            {
                findings.Add(ScenarioFinding.Err(Tier, "DRYRUN_LOAD_FAILED", string.Empty,
                    $"Loading this document fails: {ex.Message}", ex.GetType().Name));
                return findings;
            }

            findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_LOADED", string.Empty,
                $"The document loads: {scratch.Objectives?.Count ?? 0} objective(s), " +
                $"{scratch.ScenarioTimeline?.ScenarioTimelineEvents?.Count ?? 0} timeline event(s), " +
                $"{scratch.ScenarioParameters?.Injects?.Count ?? 0} inject(s)."));

            await GeneratePopulationAndReport(findings, scratch, ct);
            return findings;
        }
        finally
        {
            // Always. A dry run that could commit would not be a dry run.
            await transaction.RollbackAsync(ct);
            // The scratch rows are gone from the database but not from the change tracker, and a
            // later read on this scoped context would still see them.
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// The population, generated pool by pool exactly as ExecutionService does when an execution is
    /// created — same generator, same grouping by campaign and pool role — and then rolled back with
    /// everything else. A count that the generator produces is worth more than a count read off the
    /// document, because it is the one the exercise will actually get.
    /// </summary>
    private async Task GeneratePopulationAndReport(List<ScenarioFinding> findings, Scenario scratch, CancellationToken ct)
    {
        var pools = scratch.ScenarioParameters?.UserPools?.ToList() ?? [];
        if (pools.Count == 0)
        {
            findings.Add(ScenarioFinding.Warn(Tier, "DRYRUN_NPCS", "/population/pools",
                "The document declares no population pools, so an execution generates nobody and there is no one to animate.",
                "Give population.pools a role and a count."));
            return;
        }

        var total = 0;
        for (var i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            var path = $"/population/pools/{i}";

            if (pool.Count < 1)
            {
                findings.Add(ScenarioFinding.Warn(Tier, "DRYRUN_NPCS", path,
                    $"Pool \"{pool.Role}\" has a count of {pool.Count}, so it generates nobody."));
                continue;
            }

            int created;
            try
            {
                created = await GeneratePoolAsync(scratch, pool, ct);
            }
            catch (Exception ex)
            {
                findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_POPULATION_SKIPPED", path,
                    $"The population was not generated, so these counts are the document's rather than the generator's: {ex.Message}",
                    ex.GetType().Name));
                return;
            }

            total += created;
            findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_NPCS", path,
                $"Pool \"{pool.Role}\" generates {created} of {pool.Count} NPC(s)."));
        }

        if (total > 0)
        {
            findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_READINESS", string.Empty,
                $"{total} NPC(s) across {pools.Count(p => p.Count > 0)} pool(s) would need machines assigned before deployment."));
        }
    }

    /// <summary>
    /// One pool's NPCs, as GenerateUserPoolNpcsAsync makes them, minus the execution: a dry run has no
    /// run to scope them to, and cohort linking says nothing about whether the pool generates.
    /// </summary>
    private async Task<int> GeneratePoolAsync(Scenario scratch, UserPool pool, CancellationToken ct)
    {
        var created = 0;
        for (var i = 0; i < pool.Count; i++)
        {
            var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
            npc.Id = npc.NpcProfile.Id;
            npc.CreatedUtc = DateTime.UtcNow;
            npc.ScenarioId = scratch.Id;
            npc.Campaign = scratch.Name;
            npc.Team = pool.Role;

            context.Npcs.Add(npc);
            created++;
        }

        await context.SaveChangesAsync(ct);
        return created;
    }
}
