// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The n8n workflows that animate NPCs ask for one execution's population: its pool NPCs and its scenario's
/// compiled NPCs, never another run's. "Unbound" narrows that to NPCs with no machine, the ones that only post.
/// </summary>
public class NpcListScopingTests
{
    [Fact]
    public async Task An_execution_filter_returns_its_pool_and_its_scenarios_compiled_npcs_only()
    {
        await using var context = TestDb.New("npc-list");
        var (runA, runB) = await TwoRuns(context);
        var poolA = AddNpc(context, scenarioId: runA.ScenarioId, executionId: runA.Id);
        var compiled = AddNpc(context, scenarioId: runA.ScenarioId, executionId: null);
        AddNpc(context, scenarioId: runB.ScenarioId, executionId: runB.Id);
        AddNpc(context, scenarioId: null, executionId: null);

        var list = await Service(context).GetListAsync(runA.Id, unbound: false);

        Assert.Equal(new[] { poolA, compiled }.Order(), list.Select(n => n.Id).Order());
    }

    [Fact]
    public async Task Unbound_leaves_out_npcs_that_have_a_machine()
    {
        await using var context = TestDb.New("npc-list");
        var (runA, _) = await TwoRuns(context);
        var visitor = AddNpc(context, runA.ScenarioId, runA.Id);
        AddNpc(context, runA.ScenarioId, runA.Id, machineId: Guid.NewGuid());

        var list = await Service(context).GetListAsync(runA.Id, unbound: true);

        Assert.Equal(visitor, Assert.Single(list).Id);
    }

    [Fact]
    public async Task With_no_filter_every_npc_is_listed()
    {
        await using var context = TestDb.New("npc-list");
        var (runA, runB) = await TwoRuns(context);
        AddNpc(context, runA.ScenarioId, runA.Id);
        AddNpc(context, runB.ScenarioId, runB.Id);
        AddNpc(context, null, null);

        var list = await Service(context).GetListAsync(null, unbound: false);

        Assert.Equal(3, list.Count());
    }

    private static NpcService Service(ApplicationDbContext context) => new(context, new Mock<IEvidenceProcessor>().Object);

    private static async Task<(Execution, Execution)> TwoRuns(ApplicationDbContext context)
    {
        var fair = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var other = new Scenario { Name = "Other", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Scenarios.AddRange(fair, other);
        await context.SaveChangesAsync();
        var runA = new Execution { ScenarioId = fair.Id, Name = "office-r1", CreatedAt = DateTime.UtcNow };
        var runB = new Execution { ScenarioId = other.Id, Name = "other-r1", CreatedAt = DateTime.UtcNow };
        context.Executions.AddRange(runA, runB);
        await context.SaveChangesAsync();
        return (runA, runB);
    }

    private static Guid AddNpc(ApplicationDbContext context, int? scenarioId, int? executionId, Guid? machineId = null)
    {
        var npc = new NpcRecord
        {
            Id = Guid.NewGuid(), NpcProfile = new NpcProfile(), ScenarioId = scenarioId, ExecutionId = executionId,
            MachineId = machineId, CreatedUtc = DateTime.UtcNow
        };
        context.Npcs.Add(npc);
        context.SaveChanges();
        return npc.Id;
    }
}
