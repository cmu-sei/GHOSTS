// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// A run keeps its own ground truth. It starts from the document's starting conditions; completed items apply
/// their effects; White Cell can set and clear flags and record a decision; a triggered item comes due only when
/// its condition holds; and an objective is met once its condition does.
/// </summary>
public class RunStateTests
{
    private readonly Mock<IClientHubService> _clientHub = new();
    private readonly Mock<IHubContext<ExecutionHub>> _hub = new();

    public RunStateTests() =>
        _clientHub.Setup(h => h.SendUpdate(It.IsAny<Guid>(), It.IsAny<MachineUpdate>())).ReturnsAsync(true);

    [Fact]
    public async Task Starting_a_run_seeds_its_flags_and_facts_from_the_starting_conditions()
    {
        await using var context = TestDb.New("runstate");
        var scenario = await Scenario(context, extras: """{"startingConditions":{"flags":["office-open"],"facts":{"floor":"3"}}}""");
        var service = Executions(context);
        var execution = await service.CreateAsync(new CreateExecutionDto(scenario.Id, "r1", null, null, null), default);

        await service.StartAsync(execution.Id, default);
        var state = await States(context).GetAsync(execution.Id, default);

        Assert.Equal(["office-open"], state.Flags);
        Assert.Equal("3", state.Facts["floor"]);
    }

    [Fact]
    public async Task A_triggered_item_comes_due_only_once_its_condition_holds()
    {
        await using var context = TestDb.New("runstate");
        var scenario = await Scenario(context, events: Event(1, "T+0m", "White Cell", "Serve the coffee", when: "flag:coffee-ready"));
        var execution = await Running(context, scenario.Id);

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);
        var before = Item(context, execution.Id, 1).Status;
        await States(context).SetFlagsAsync(execution.Id, new SetFlagsDto(["coffee-ready"], null, "white-cell"), default);
        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Equal("Pending", before);
        Assert.Equal("Ready", Item(context, execution.Id, 1).Status);
    }

    [Fact]
    public async Task Completing_an_item_applies_the_effects_of_its_event()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "Brew the coffee");
        evt.Extras = """{"effects":{"setFlags":["coffee-ready"],"setFacts":{"pot":"full"}}}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        await Executions(context).CompleteTimelineItemAsync(execution.Id, item.Id, new CompleteTimelineItemDto("Completed", null, "white-cell"), default);
        var state = await States(context).GetAsync(execution.Id, default);

        Assert.Contains("coffee-ready", state.Flags);
        Assert.Equal("full", state.Facts["pot"]);
    }

    [Fact]
    public async Task A_skipped_item_applies_nothing()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "Brew the coffee");
        evt.Extras = """{"effects":{"setFlags":["coffee-ready"]}}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        await Executions(context).CompleteTimelineItemAsync(execution.Id, item.Id, new CompleteTimelineItemDto("Skipped", null, "white-cell"), default);

        Assert.Empty((await States(context).GetAsync(execution.Id, default)).Flags);
    }

    [Fact]
    public async Task A_client_reported_completion_applies_the_events_effects()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "Green Cell", "Staff refill the printer");
        evt.Extras = """{"effects":{"setFlags":["printer-full"]}}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        await Executions(context).RecordScenarioActionAsync(execution.Id, item.Id, null, "done", default);

        Assert.Equal(["printer-full"], (await States(context).GetAsync(execution.Id, default)).Flags);
    }

    [Fact]
    public async Task White_cell_can_set_and_clear_flags()
    {
        await using var context = TestDb.New("runstate");
        var scenario = await Scenario(context);
        var execution = await Running(context, scenario.Id);
        var states = States(context);

        await states.SetFlagsAsync(execution.Id, new SetFlagsDto(["coffee-ready", "printer-low"], null, "white-cell"), default);
        var state = await states.SetFlagsAsync(execution.Id, new SetFlagsDto(null, ["printer-low"], "white-cell"), default);

        Assert.Equal(["coffee-ready"], state.Flags);
        Assert.Contains(context.ExecutionEvents, e => e.ExecutionId == execution.Id && e.EventType == "FlagsChanged");
    }

    [Fact]
    public async Task Recording_a_decision_applies_the_chosen_answers_effects_and_completes_the_item()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "Pick where the team has lunch");
        evt.Extras = """{"choices":[{"id":"cafe","label":"The cafe","effects":{"setFlags":["lunch-at-cafe"]}},{"id":"kitchen","label":"The kitchen","effects":{"setFlags":["lunch-in-kitchen"]}}]}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        var state = await States(context).RecordDecisionAsync(execution.Id, item.Id, new RecordDecisionDto("cafe", "white-cell"), default);

        Assert.Equal(["lunch-at-cafe"], state.Flags);
        var decision = Assert.Single(state.Decisions);
        Assert.Equal("cafe", decision.ChoiceId);
        Assert.Equal("The cafe", decision.Label);
        Assert.Equal("Completed", Item(context, execution.Id, 1).Status);
    }

    [Fact]
    public async Task A_decision_must_name_one_of_the_items_choices()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "Pick where the team has lunch");
        evt.Extras = """{"choices":[{"id":"cafe","label":"The cafe"}]}""";
        var plain = Event(2, "T+0m", "White Cell", "Staff arrive");
        var scenario = await Scenario(context, events: [evt, plain]);
        var execution = await Running(context, scenario.Id);
        var states = States(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            states.RecordDecisionAsync(execution.Id, Item(context, execution.Id, 1).Id, new RecordDecisionDto("picnic", "white-cell"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            states.RecordDecisionAsync(execution.Id, Item(context, execution.Id, 2).Id, new RecordDecisionDto("cafe", "white-cell"), default));
    }

    [Fact]
    public async Task An_objective_is_met_once_its_condition_holds()
    {
        await using var context = TestDb.New("runstate");
        var scenario = await Scenario(context);
        context.ScenarioDocuments.Add(new ScenarioDocument
        {
            ScenarioId = scenario.Id, Origin = ScenarioDocument.Imported, CreatedAt = DateTime.UtcNow,
            Document = """{"assessment":{"objectives":[{"id":1,"name":"Coffee is served","metWhen":"flag:coffee-ready"},{"id":2,"name":"Lunch is decided","metWhen":"flag:lunch-at-cafe"}]}}"""
        });
        await context.SaveChangesAsync();
        var execution = await Running(context, scenario.Id);

        var state = await States(context).SetFlagsAsync(execution.Id, new SetFlagsDto(["coffee-ready"], null, "white-cell"), default);

        Assert.True(state.Objectives.Single(o => o.Id == 1).Met);
        Assert.False(state.Objectives.Single(o => o.Id == 2).Met);
        Assert.Equal("Coffee is served", state.Objectives.Single(o => o.Id == 1).Name);
    }

    [Fact]
    public async Task White_cell_marks_an_expected_response_met_or_missed()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "The printer runs out of paper");
        evt.Extras = """{"expectedResponse":"Staff refill the printer within ten minutes"}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        var state = await States(context).MarkResponseAsync(execution.Id, item.Id, new MarkResponseDto(true, "Refilled in six minutes", "white-cell"), default);

        var mark = Assert.Single(state.Responses);
        Assert.Equal((item.Id, 1, "Staff refill the printer within ten minutes", true, "Refilled in six minutes", "white-cell"),
            (mark.ItemId, mark.ItemNumber, mark.Expected, mark.Met, mark.Notes, mark.By));
        Assert.Contains(context.ExecutionEvents, e => e.ExecutionId == execution.Id && e.EventType == "ResponseMarked");
    }

    [Fact]
    public async Task Marking_a_response_again_replaces_the_earlier_mark()
    {
        await using var context = TestDb.New("runstate");
        var evt = Event(1, "T+0m", "White Cell", "The printer runs out of paper");
        evt.Extras = """{"expectedResponse":"Staff refill the printer"}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);
        var states = States(context);

        await states.MarkResponseAsync(execution.Id, item.Id, new MarkResponseDto(true, null, "white-cell"), default);
        var state = await states.MarkResponseAsync(execution.Id, item.Id, new MarkResponseDto(false, "Nobody noticed", "white-cell"), default);

        Assert.False(Assert.Single(state.Responses).Met);
    }

    [Fact]
    public async Task An_item_with_no_expected_response_cannot_be_marked()
    {
        await using var context = TestDb.New("runstate");
        var scenario = await Scenario(context, events: Event(1, "T+0m", "White Cell", "Open the office"));
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            States(context).MarkResponseAsync(execution.Id, item.Id, new MarkResponseDto(true, null, "white-cell"), default));
    }

    [Fact]
    public async Task A_client_report_applied_to_a_stale_copy_of_the_run_keeps_a_flag_white_cell_set_meanwhile()
    {
        var db = $"runstate-{Guid.NewGuid()}";
        await using var context = TestDb.Named(db);
        var evt = Event(1, "T+0m", "Green Cell", "Staff refill the printer");
        evt.Extras = """{"effects":{"setFlags":["printer-full"]}}""";
        var scenario = await Scenario(context, events: evt);
        var execution = await Running(context, scenario.Id);
        var item = Item(context, execution.Id, 1);
        await using (var other = TestDb.Named(db))
            await States(other).SetFlagsAsync(execution.Id, new SetFlagsDto(["coffee-ready"], null, "white-cell"), default);

        await Executions(context).RecordScenarioActionAsync(execution.Id, item.Id, null, "done", default);

        await using var check = TestDb.Named(db);
        Assert.Equal(["coffee-ready", "printer-full"], (await States(check).GetAsync(execution.Id, default)).Flags.Order());
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private ExecutionService Executions(ApplicationDbContext context) => new(context, _clientHub.Object, _hub.Object);
    private ExecutionStateService States(ApplicationDbContext context) => new(context, _hub.Object);
    private ExecutionDispatcher Dispatcher(ApplicationDbContext context) => new(context, _clientHub.Object, _hub.Object);

    private static ScenarioTimelineEvent Event(int number, string time, string assigned, string description, string? when = null) => new()
    {
        Number = number, Time = time, Assigned = assigned, Description = description, ObjectiveIds = "[]",
        TriggerKind = when == null ? TriggerKind.PointInTime : TriggerKind.Triggered, TriggerCondition = when
    };

    private static async Task<Scenario> Scenario(ApplicationDbContext context, string? extras = null, params ScenarioTimelineEvent[] events)
    {
        var scenario = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, Extras = extras };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var timeline = new ScenarioTimeline { ScenarioId = scenario.Id };
        foreach (var e in events) timeline.ScenarioTimelineEvents.Add(e);
        context.ScenarioTimelines.Add(timeline);
        await context.SaveChangesAsync();
        return scenario;
    }

    private async Task<Execution> Running(ApplicationDbContext context, int scenarioId)
    {
        var service = Executions(context);
        var execution = await service.CreateAsync(new CreateExecutionDto(scenarioId, null, null, null, null), default);
        await service.StartAsync(execution.Id, default);
        return context.Executions.Single(e => e.Id == execution.Id);
    }

    private static ExecutionTimelineItem Item(ApplicationDbContext context, int executionId, int number) =>
        context.ExecutionTimelineItems.AsNoTracking().Single(i => i.ExecutionId == executionId && i.Number == number);
}
