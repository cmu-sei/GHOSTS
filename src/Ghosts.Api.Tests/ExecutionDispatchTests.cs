// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Models;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The server, not the client, decides when a timeline item happens. Each item is dispatched once, when its
/// time comes, as a one-shot update to the clients of the NPCs bound to the run. Starting a run sends nothing;
/// pausing stops the clock; resuming replays nothing.
/// </summary>
public class ExecutionDispatchTests
{
    private readonly Mock<IClientHubService> _clientHub = new();
    private readonly List<(Guid Machine, MachineUpdate Update)> _sent = [];

    public ExecutionDispatchTests()
    {
        _clientHub.Setup(h => h.SendUpdate(It.IsAny<Guid>(), It.IsAny<MachineUpdate>()))
            .Callback<Guid, MachineUpdate>((m, u) => _sent.Add((m, u)))
            .ReturnsAsync(true);
    }

    // ── what each item is, decided once at create ─────────────────────────

    [Fact]
    public async Task Each_item_is_dispatched_to_npcs_run_by_hand_or_sent_to_a_workflow_by_its_owner_and_mode()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context,
            Event(1, "T+5m", "Green Cell", "Staff check their email"),
            Event(2, "T+10m", "White Cell", "Announce the team lunch"),
            Event(3, "T+15m", "Blue Team", "Help desk logs a ticket"),
            Event(4, "T+20m", "", "Staff print the agenda"),
            Event(5, "T+25m", "White Cell", "Refresh the social graph", ExecutionType.Workflow, "social_graph"));

        var execution = await Service(context).CreateAsync(new CreateExecutionDto(scenario.Id, "r1", null, null, null), default);

        var kinds = context.ExecutionTimelineItems.Where(i => i.ExecutionId == execution.Id)
            .OrderBy(i => i.Number).Select(i => i.AutomationKind).ToList();
        Assert.Equal(["MachineUpdate", "Manual", "Manual", "MachineUpdate", "Workflow"], kinds);
    }

    [Fact]
    public async Task An_items_effects_and_expected_response_are_copied_onto_it_at_create()
    {
        await using var context = TestDb.New("dispatch");
        var evt = Event(1, "T+5m", "Green Cell", "The printer runs low on toner");
        evt.Extras = """{"id":"printer-low","expectedResponse":"A ticket within 20 minutes","effects":{"setFlags":["printer-low"]}}""";
        var scenario = await PublishedScenario(context, evt);

        var execution = await Service(context).CreateAsync(new CreateExecutionDto(scenario.Id, "r1", null, null, null), default);

        var item = context.ExecutionTimelineItems.Single(i => i.ExecutionId == execution.Id);
        var extras = ScenarioExtras.Read<EventExtrasDto>(item.Extras);
        Assert.Equal("printer-low", extras.Id);
        Assert.Equal("A ticket within 20 minutes", extras.ExpectedResponse);
        Assert.Equal(["printer-low"], extras.Effects.SetFlags);
    }

    [Fact]
    public async Task Injects_become_items_numbered_after_the_events_and_before_the_workflow_bindings()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+5m", "White Cell", "Staff arrive"));
        var parameters = new ScenarioParameters { ScenarioId = scenario.Id };
        parameters.Injects.Add(new Inject { Trigger = "T+30m", Title = "The coffee machine is restocked", Extras = """{"owner":"white-cell"}""" });
        parameters.WorkflowBindings.Add(new ScenarioWorkflowBinding { WorkflowRef = "beliefs", DisplayName = "Beliefs", Cron = "*/15 * * * *", Enabled = true });
        context.ScenarioParameters.Add(parameters);
        await context.SaveChangesAsync();

        var execution = await Service(context).CreateAsync(new CreateExecutionDto(scenario.Id, "r1", null, null, null), default);

        var items = context.ExecutionTimelineItems.Where(i => i.ExecutionId == execution.Id).OrderBy(i => i.Number).ToList();
        Assert.Equal([1, 2, 3], items.Select(i => i.Number));
        Assert.Equal("The coffee machine is restocked", items[1].Description);
        Assert.Equal("T+30m", items[1].Time);
        Assert.Equal("Manual", items[1].AutomationKind);
        Assert.Equal("White Cell", items[1].Assigned);
        Assert.Equal("Animation workflow: Beliefs", items[2].Description);
    }

    // ── starting sends nothing ───────────────────────────────────────────

    [Fact]
    public async Task Starting_a_run_sends_nothing_to_clients()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+0m", "Green Cell", "Staff arrive"));
        var service = Service(context);
        var execution = await service.CreateAsync(new CreateExecutionDto(scenario.Id, "r1", null, null, null), default);
        BindCompiledNpc(context, scenario.Id);

        await service.StartAsync(execution.Id, default);

        Assert.Empty(_sent);
        Assert.Empty(context.MachineUpdates);
    }

    [Fact]
    public async Task An_npc_assigned_in_the_latest_compilation_counts_as_bound()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);
        var machine = BindCompiledNpc(context, scenario.Id);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Equal(machine, Assert.Single(_sent).Machine);
    }

    // ── dispatch when due ────────────────────────────────────────────────

    [Fact]
    public async Task A_due_npc_item_goes_to_each_bound_machine_as_one_one_shot_handler_and_a_later_one_waits()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context,
            Event(1, "T+10m", "Green Cell", "Staff check their email"),
            Event(2, "T+1h", "Green Cell", "Staff go to lunch"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 15);
        var machineA = BindNpc(context, execution.Id);
        var machineB = BindNpc(context, execution.Id);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Equal(2, _sent.Count);
        Assert.Equal(new[] { machineA, machineB }.Order(), _sent.Select(s => s.Machine).Order());
        var item1 = Item(context, execution.Id, 1);
        foreach (var (_, update) in _sent)
        {
            Assert.Equal(UpdateClientConfig.UpdateType.TimelinePartial, update.Type);
            var handler = Assert.Single(update.Update.TimeLineHandlers);
            Assert.Equal(HandlerType.NpcSystem, handler.HandlerType);
            Assert.False(handler.Loop);
            var e = Assert.Single(handler.TimeLineEvents);
            Assert.Equal("npc-scenario-action", e.Command);
            Assert.Equal(0, e.DelayBefore);
            Assert.Contains($"item:{item1.Id}", e.CommandArgs.Select(a => a.ToString()));
        }
        Assert.Equal("Deployed", item1.Status);
        Assert.Equal(1, item1.FireCount);
        Assert.Equal("Pending", Item(context, execution.Id, 2).Status);
    }

    [Fact]
    public async Task A_dispatched_item_is_not_sent_again_on_the_next_poll()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);
        BindNpc(context, execution.Id);

        await Dispatcher(context).DispatchDueAsync(Now, default);
        await Dispatcher(context).DispatchDueAsync(Now.AddSeconds(10), default);

        Assert.Single(_sent);
    }

    [Fact]
    public async Task Paused_time_does_not_count_toward_an_items_offset()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+25m", "Green Cell", "Staff take a break"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 30);
        execution.PausedSeconds = 10 * 60;
        await context.SaveChangesAsync();
        BindNpc(context, execution.Id);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Empty(_sent);
        Assert.Equal("Pending", Item(context, execution.Id, 1).Status);
    }

    [Fact]
    public void The_exercise_clock_stops_when_the_run_ends()
    {
        var start = new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
        var execution = new Execution { StartedAt = start, CompletedAt = start.AddHours(2), PausedSeconds = 10 * 60 };

        Assert.Equal(110, ExecutionClock.ElapsedMinutes(execution, start.AddHours(5)));
    }

    [Fact]
    public async Task Nothing_is_dispatched_while_a_run_is_paused()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);
        BindNpc(context, execution.Id);
        await Service(context).PauseAsync(execution.Id, default);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Empty(_sent);
    }

    [Fact]
    public async Task Resuming_adds_the_pause_to_paused_time_and_replays_nothing_already_sent()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var service = Service(context);
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);
        BindNpc(context, execution.Id);
        await Dispatcher(context).DispatchDueAsync(Now, default);
        await service.PauseAsync(execution.Id, default);
        execution.PausedAt = DateTime.UtcNow.AddMinutes(-3);
        await context.SaveChangesAsync();

        await service.StartAsync(execution.Id, default);
        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Single(_sent);
        Assert.Null(execution.PausedAt);
        Assert.InRange(execution.PausedSeconds, 170, 200);
    }

    [Fact]
    public async Task A_due_white_cell_item_becomes_ready_and_reaches_no_client()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+5m", "White Cell", "Announce the team lunch"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 10);
        BindNpc(context, execution.Id);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Empty(_sent);
        Assert.Equal("Ready", Item(context, execution.Id, 1).Status);
    }

    [Fact]
    public async Task A_due_npc_item_with_no_bound_npcs_becomes_ready_for_white_cell()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+5m", "Green Cell", "Staff check their email"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 10);

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Equal("Ready", Item(context, execution.Id, 1).Status);
        Assert.Contains(context.ExecutionEvents, e => e.ExecutionId == execution.Id && e.EventType == "NoBoundNpcs");
    }

    [Fact]
    public async Task A_scheduled_npc_item_fires_on_its_cron_from_the_server_and_stays_active()
    {
        await using var context = TestDb.New("dispatch");
        var evt = Event(1, "T+0m", "Green Cell", "Staff refill their water bottles");
        evt.TriggerKind = TriggerKind.Scheduled;
        evt.Schedule = "*/10 * * * *";
        var scenario = await PublishedScenario(context, evt);
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 0, startedAt: new DateTime(2026, 10, 28, 9, 3, 0, DateTimeKind.Utc));
        BindNpc(context, execution.Id);
        var dispatcher = Dispatcher(context);

        await dispatcher.DispatchDueAsync(new DateTime(2026, 10, 28, 9, 5, 0, DateTimeKind.Utc), default);
        var beforeFirst = _sent.Count;
        await dispatcher.DispatchDueAsync(new DateTime(2026, 10, 28, 9, 10, 5, DateTimeKind.Utc), default);
        var afterFirst = _sent.Count;
        await dispatcher.DispatchDueAsync(new DateTime(2026, 10, 28, 9, 15, 0, DateTimeKind.Utc), default);
        var stillAfterFirst = _sent.Count;
        await dispatcher.DispatchDueAsync(new DateTime(2026, 10, 28, 9, 20, 1, DateTimeKind.Utc), default);

        Assert.Equal(0, beforeFirst);
        Assert.Equal(1, afterFirst);
        Assert.Equal(1, stillAfterFirst);
        Assert.Equal(2, _sent.Count);
        var item = Item(context, execution.Id, 1);
        Assert.Equal(2, item.FireCount);
        Assert.Equal("Deployed", item.Status);
        Assert.False(_sent[0].Update.Update.TimeLineHandlers[0].Loop);
    }

    // ── the run ends at its duration ─────────────────────────────────────

    [Fact]
    public async Task When_the_duration_passes_unfired_items_are_skipped_and_the_run_completes()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context,
            Event(1, "T+10m", "White Cell", "Staff arrive"),
            Event(2, "T+3h", "White Cell", "An optional item nobody used"));
        context.GameMechanics.Add(new GameMechanics { ScenarioId = scenario.Id, DurationHours = 4 });
        await context.SaveChangesAsync();
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 4 * 60 + 1);
        var first = context.ExecutionTimelineItems.Single(i => i.ExecutionId == execution.Id && i.Number == 1);
        first.Status = "Completed";
        await context.SaveChangesAsync();

        await Dispatcher(context).DispatchDueAsync(Now, default);

        Assert.Equal(ExecutionStatus.Completed, context.Executions.Single(e => e.Id == execution.Id).Status);
        Assert.Equal("Completed", Item(context, execution.Id, 1).Status);
        Assert.Equal("Skipped", Item(context, execution.Id, 2).Status);
    }

    // ── client reports ───────────────────────────────────────────────────

    [Fact]
    public async Task A_report_naming_an_item_completes_that_item_not_another_with_the_same_number()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var service = Service(context);
        var runA = await Running(context, scenario.Id, startedMinutesAgo: 5);
        var runB = await Running(context, scenario.Id, startedMinutesAgo: 5);
        var itemB = Item(context, runB.Id, 1);

        await service.RecordScenarioActionAsync(runB.Id, itemB.Id, null, $"execution:{runB.Id},item:{itemB.Id}", default);

        Assert.Equal("Completed", Item(context, runB.Id, 1).Status);
        Assert.Equal("Pending", Item(context, runA.Id, 1).Status);
    }

    [Fact]
    public async Task A_report_for_a_finished_run_changes_nothing()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var service = Service(context);
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);
        await service.StopAsync(execution.Id, false, default);
        var item = Item(context, execution.Id, 1);

        await service.RecordScenarioActionAsync(execution.Id, item.Id, null, "late report", default);

        Assert.Equal("Pending", Item(context, execution.Id, 1).Status);
        Assert.Equal(0, Item(context, execution.Id, 1).FireCount);
    }

    [Fact]
    public async Task A_legacy_report_with_only_an_event_number_still_completes_its_item()
    {
        await using var context = TestDb.New("dispatch");
        var scenario = await PublishedScenario(context, Event(1, "T+1m", "Green Cell", "Staff arrive"));
        var execution = await Running(context, scenario.Id, startedMinutesAgo: 5);

        await Service(context).RecordScenarioActionAsync(execution.Id, null, 1, "execution:1,event:1", default);

        Assert.Equal("Completed", Item(context, execution.Id, 1).Status);
    }

    // ── offsets ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("T+30d", 43200)]
    [InlineData("T+100d", 144000)]
    public void Offsets_in_minutes_do_not_overflow_past_24_days(string time, long minutes)
    {
        Assert.Equal(minutes, ExecutionService.ParseTimeOffsetMinutes(time));
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static readonly DateTime Now = DateTime.UtcNow;

    private ExecutionService Service(ApplicationDbContext context) =>
        new(context, _clientHub.Object, new Mock<IHubContext<ExecutionHub>>().Object);

    private ExecutionDispatcher Dispatcher(ApplicationDbContext context) =>
        new(context, _clientHub.Object, new Mock<IHubContext<ExecutionHub>>().Object);

    private static ScenarioTimelineEvent Event(int number, string time, string assigned, string description,
        ExecutionType type = ExecutionType.Manual, string? workflowId = null) => new()
    {
        Number = number,
        Time = time,
        Assigned = assigned,
        Description = description,
        ExecutionType = type,
        WorkflowId = workflowId,
        ObjectiveIds = "[]"
    };

    private static async Task<Scenario> PublishedScenario(ApplicationDbContext context, params ScenarioTimelineEvent[] events)
    {
        var scenario = new Scenario { Name = "Office Day", Description = "An ordinary day at the office", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var timeline = new ScenarioTimeline { ScenarioId = scenario.Id };
        foreach (var e in events) timeline.ScenarioTimelineEvents.Add(e);
        context.ScenarioTimelines.Add(timeline);
        await context.SaveChangesAsync();
        return scenario;
    }

    private async Task<Execution> Running(ApplicationDbContext context, int scenarioId, int startedMinutesAgo, DateTime? startedAt = null)
    {
        var execution = await Service(context).CreateAsync(new CreateExecutionDto(scenarioId, null, null, null, null), default);
        var tracked = context.Executions.Single(e => e.Id == execution.Id);
        tracked.Status = ExecutionStatus.Running;
        tracked.StartedAt = startedAt ?? Now.AddMinutes(-startedMinutesAgo);
        await context.SaveChangesAsync();
        return tracked;
    }

    private static Guid BindNpc(ApplicationDbContext context, int executionId)
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = $"desk-{Guid.NewGuid():N}"[..13] };
        context.Machines.Add(machine);
        context.Npcs.Add(new NpcRecord
        {
            Id = Guid.NewGuid(),
            NpcProfile = new NpcProfile(),
            ExecutionId = executionId,
            MachineId = machine.Id,
            Team = "office staff",
            CreatedUtc = DateTime.UtcNow
        });
        context.SaveChanges();
        return machine.Id;
    }

    private static Guid BindCompiledNpc(ApplicationDbContext context, int scenarioId)
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = $"laptop-{Guid.NewGuid():N}"[..14] };
        context.Machines.Add(machine);
        var compilation = new ScenarioCompilation { ScenarioId = scenarioId, Name = "Build 1", Status = "Completed", CompletedAt = DateTime.UtcNow };
        context.ScenarioCompilations.Add(compilation);
        context.SaveChanges();
        context.ScenarioNpcAssignments.Add(new ScenarioNpcAssignment { ScenarioId = scenarioId, CompilationId = compilation.Id, NpcId = Guid.NewGuid(), MachineId = machine.Id });
        context.SaveChanges();
        return machine.Id;
    }

    private static ExecutionTimelineItem Item(ApplicationDbContext context, int executionId, int number) =>
        context.ExecutionTimelineItems.AsNoTracking().Single(i => i.ExecutionId == executionId && i.Number == number);
}
