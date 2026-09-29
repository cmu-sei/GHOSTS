// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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
/// Tier 4: would this document actually load? The only way to know is to load it, so the document is
/// mapped, created and compiled inside a transaction that is rolled back whatever happens. Nothing
/// survives — the scenario count is the same before and after — and every exception on the way becomes
/// a finding instead of a stack trace. This is the cheap half of tier 4; tier 5, checking a deployed
/// exercise against its document, is not built.
/// </summary>
public class ScenarioDryRunService(
    ApplicationDbContext context,
    IScenarioService scenarios,
    IScenarioCompilerService compiler) : IScenarioDryRunService
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

            await CompileAndReport(findings, scratch, ct);
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

    private async Task CompileAndReport(List<ScenarioFinding> findings, Scenario scratch, CancellationToken ct)
    {
        ScenarioCompilation compilation;
        try
        {
            compilation = await compiler.CompileAsync(
                scratch.Id, new CompileScenarioDto($"dry-run {scratch.Name}"), ct);
        }
        catch (Exception ex)
        {
            // The compile path needs no content or LLM service, so a failure here is the document's,
            // not a missing dependency's — but it is still only a warning: an import does not compile.
            findings.Add(ScenarioFinding.Warn(Tier, "DRYRUN_COMPILE_FAILED", string.Empty,
                $"The document loads but does not compile: {ex.Message}", ex.GetType().Name));
            return;
        }

        if (!string.Equals(compilation.Status, "Completed", StringComparison.Ordinal))
        {
            findings.Add(ScenarioFinding.Warn(Tier, "DRYRUN_COMPILE_INCOMPLETE", string.Empty,
                $"Compilation ended as \"{compilation.Status}\": {compilation.ErrorMessage ?? "no reason given"}."));
        }

        findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_COMPILED", string.Empty,
            $"Compiles to {compilation.NpcCount} NPC(s), {compilation.TimelineEventCount} timeline event(s), " +
            $"{compilation.InjectCount} inject(s)."));

        // Readiness, as GET compilations/{id}/readiness computes it. The unassigned-NPC count is
        // always the full count here: assigning NPCs to machines is a later, deliberate step, so it
        // is reported as a fact about the compile and not as a defect in the document.
        var npcs = await context.ScenarioEntities
            .CountAsync(e => e.ScenarioId == scratch.Id && e.EntityType == "Person" && e.NpcId != null, ct);
        if (npcs == 0)
        {
            findings.Add(ScenarioFinding.Warn(Tier, "DRYRUN_NO_NPCS", "/population/pools",
                "Compiling this document generates no NPCs, so there is nobody to animate.",
                "Give population.pools a role and a count, or add Person entities."));
        }
        else
        {
            findings.Add(ScenarioFinding.Note(Tier, "DRYRUN_READINESS", string.Empty,
                $"{npcs} NPC(s) would need machines assigned before deployment."));
        }
    }
}
