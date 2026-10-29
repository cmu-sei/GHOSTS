// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Models;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Api.Infrastructure.Terrain;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// An NPC bound to a host by name is reached once a client with that name registers. An item with an NPC action
/// sends the action's real handler, then the report that completes the item, and only to the NPCs its targets name.
/// </summary>
public class ActionDispatchTests
{
    private readonly Mock<IClientHubService> _clientHub = new();
    private readonly List<(Guid Machine, MachineUpdate Update)> _sent = [];

    public ActionDispatchTests() =>
        _clientHub.Setup(h => h.SendUpdate(It.IsAny<Guid>(), It.IsAny<MachineUpdate>()))
            .Callback<Guid, MachineUpdate>((m, u) => _sent.Add((m, u)))
            .ReturnsAsync(true);

    private const string Terrain = """
        {"terrain":{"segments":[{"name":"Office LAN"},{"name":"Front desk"}],
                    "hosts":[{"name":"desk-01","segment":"Office LAN"},{"name":"desk-02","segment":"Office LAN"},{"name":"lobby-01","segment":"Front desk"}]}}
        """;

    [Fact]
    public async Task An_npc_bound_by_host_name_is_reached_once_a_client_with_that_name_registers()
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff check the news"));
        var npc = Npc(context, execution.Id, "office staff", "desk-01");

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);
        var before = (_sent.Count, ItemOf(context, execution.Id, 1).Status);
        ResetItem(context, execution.Id, 1);
        var machine = Machine(context, "DESK-01");
        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Equal((0, "Ready"), before);
        Assert.Equal(machine, Assert.Single(_sent).Machine);
        Assert.Equal(machine, context.Npcs.AsNoTracking().Single(n => n.Id == npc).MachineId);
    }

    [Fact]
    public async Task An_npc_action_is_sent_as_its_handler_with_its_commands_and_arguments_followed_by_the_report()
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff read the newsletter",
            """{"execution":{"action":{"handler":"BrowserChrome","initial":"about:blank","commands":["browse"],"commandArgs":["http://intranet.office.local/news"]}}}"""));
        Npc(context, execution.Id, "office staff", "desk-01");
        Machine(context, "desk-01");

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        var handlers = Assert.Single(_sent).Update.Update.TimeLineHandlers;
        Assert.Equal([HandlerType.BrowserChrome, HandlerType.NpcSystem], handlers.Select(h => h.HandlerType));
        Assert.Equal("about:blank", handlers[0].Initial);
        var browse = Assert.Single(handlers[0].TimeLineEvents);
        Assert.Equal("browse", browse.Command);
        Assert.Equal(["http://intranet.office.local/news"], browse.CommandArgs.Select(a => a.ToString()));
        Assert.False(handlers[0].Loop);
        Assert.Equal("npc-scenario-action", Assert.Single(handlers[1].TimeLineEvents).Command);
    }

    [Theory]
    [InlineData("BrowserChrome", "random")]
    [InlineData("BrowserFirefox", "random")]
    [InlineData("Word", "create")]
    [InlineData("Excel", "create")]
    [InlineData("PowerPoint", "create")]
    [InlineData("Print", "print")]
    [InlineData("Outlook", "read")]
    [InlineData("Notepad", "create")]
    public async Task An_action_with_no_commands_gets_its_handlers_own_default_command(string handler, string command)
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff get on with the day",
            "{\"execution\":{\"action\":{\"handler\":\"" + handler + "\",\"commandArgs\":[\"C:\\\\Users\\\\Public\\\\Documents\"]}}}"));
        Npc(context, execution.Id, "office staff", "desk-01");
        Machine(context, "desk-01");

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        var action = Assert.Single(_sent).Update.Update.TimeLineHandlers[0];
        Assert.Equal(command, Assert.Single(action.TimeLineEvents).Command);
        Assert.Equal([@"C:\Users\Public\Documents"], action.TimeLineEvents[0].CommandArgs.Select(a => a.ToString()));
    }

    [Theory]
    [InlineData("""{"pools":["reception"]}""", new[] { "lobby-01" })]
    [InlineData("""{"hosts":["desk-02"]}""", new[] { "desk-02" })]
    [InlineData("""{"segments":["Office LAN"]}""", new[] { "desk-01", "desk-02" })]
    public async Task Targets_narrow_an_item_to_the_npcs_they_name(string targets, string[] reached)
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff print the agenda",
            "{\"execution\":{\"action\":{\"handler\":\"Print\"},\"targets\":" + targets + "}}"));
        var machines = new Dictionary<Guid, string>();
        foreach (var (team, host) in new[] { ("office staff", "desk-01"), ("office staff", "desk-02"), ("reception", "lobby-01") })
        {
            Npc(context, execution.Id, team, host);
            machines[Machine(context, host)] = host;
        }

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Equal(reached.Order(), _sent.Select(s => machines[s.Machine]).Order());
    }

    [Theory]
    [InlineData("""{"hosts":["desk-02"]}""")]
    [InlineData("""{"segments":["Office LAN"]}""")]
    public async Task Targets_in_the_documents_names_reach_npcs_on_the_hosts_graySpace_built(string targets)
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff print the agenda",
            "{\"execution\":{\"action\":{\"handler\":\"Print\",\"commandArgs\":[\"agenda.txt\"]},\"targets\":" + targets + "}}"));
        var tracked = context.Executions.Single(e => e.Id == execution.Id);
        tracked.TerrainStatus = TerrainStatus.Ready;
        tracked.Terrain = System.Text.Json.JsonSerializer.Serialize(new ExecutionTerrainDto(execution.Id, "grayspace", "grayspace/2026.10", "office-enclave",
            "g1", TerrainStatus.Ready, 100, [], new TerrainManifest([], [
                new ManifestHost("desk-02", "g1-desk-02", "main", "office-lan", "192.168.10.11", null, "10.0.1.11", [], ["ghosts-client"], new Dictionary<string, string>(), "ubuntu-22.04-lxc", "running"),
                new ManifestHost("lobby-01", "g1-lobby-01", "main", "front-desk", "192.168.11.10", null, "10.0.1.12", [], ["ghosts-client"], new Dictionary<string, string>(), "ubuntu-22.04-lxc", "running")
            ], [], []), null, DateTime.UtcNow), TerrainService.Stored);
        context.SaveChanges();
        var machines = new Dictionary<Guid, string>();
        foreach (var host in new[] { "g1-desk-02", "g1-lobby-01" })
        {
            Npc(context, execution.Id, "office staff", host);
            machines[Machine(context, host)] = host;
        }

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Equal(["g1-desk-02"], _sent.Select(s => machines[s.Machine]));
    }

    [Fact]
    public async Task One_run_that_cannot_be_dispatched_does_not_stop_the_others()
    {
        await using var context = TestDb.New("actions");
        var broken = await Running(context, Item(1, "Staff check the news"));
        var item = context.ExecutionTimelineItems.Single(i => i.ExecutionId == broken.Id);
        item.Time = "T+3000000000m";
        context.SaveChanges();
        var healthy = await Running(context, Item(1, "Staff print the agenda"));
        Npc(context, healthy.Id, "office staff", "desk-01");
        Machine(context, "desk-01");

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        Assert.Equal("Deployed", ItemOf(context, healthy.Id, 1).Status);
        Assert.Equal("Pending", ItemOf(context, broken.Id, 1).Status);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("stop")]
    [InlineData("cancel")]
    [InlineData("duration")]
    public async Task One_shots_queued_for_a_client_without_a_socket_are_withdrawn_when_the_run_stops_running(string how)
    {
        await using var context = TestDb.New("actions");
        var execution = await Running(context, Item(1, "Staff print the agenda"));
        Npc(context, execution.Id, "office staff", "desk-01");
        var machine = Machine(context, "desk-01");
        _clientHub.Setup(h => h.SendUpdate(machine, It.IsAny<MachineUpdate>())).ReturnsAsync(false);
        context.MachineUpdates.Add(new MachineUpdate { MachineId = machine, Status = StatusType.Active, Type = UpdateClientConfig.UpdateType.TimelinePartial });
        context.SaveChanges();

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);
        var queued = context.MachineUpdates.Count(u => u.ExecutionId == execution.Id);
        var service = new ExecutionService(context, _clientHub.Object, new Mock<IHubContext<ExecutionHub>>().Object);
        switch (how)
        {
            case "pause": await service.PauseAsync(execution.Id, default); break;
            case "stop": await service.StopAsync(execution.Id, false, default); break;
            case "cancel": await service.CancelAsync(execution.Id, default); break;
            case "duration":
                context.ScenarioTimelines.Single(t => t.ScenarioId == execution.ScenarioId).ExerciseDuration = 1;
                context.SaveChanges();
                await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow.AddHours(2), default);
                break;
        }

        Assert.Equal(1, queued);
        Assert.Equal(0, context.MachineUpdates.Count(u => u.ExecutionId == execution.Id));
        Assert.Equal(1, context.MachineUpdates.Count(u => u.MachineId == machine));
    }

    [Fact]
    public async Task Objectives_the_dispatcher_marks_met_do_not_overwrite_a_flag_set_while_it_sends()
    {
        var db = $"actions-{Guid.NewGuid()}";
        await using var context = TestDb.Named(db);
        var execution = await Running(context, Item(1, "Staff print the agenda"));
        context.ScenarioDocuments.Add(new ScenarioDocument
        {
            ScenarioId = execution.ScenarioId, Origin = ScenarioDocument.Imported, CreatedAt = DateTime.UtcNow,
            Document = """{"assessment":{"objectives":[{"id":1,"name":"The day is under way","metWhen":"clock>=0"}]}}"""
        });
        context.SaveChanges();
        Npc(context, execution.Id, "office staff", "desk-01");
        var machine = Machine(context, "desk-01");
        _clientHub.Setup(h => h.SendUpdate(machine, It.IsAny<MachineUpdate>())).Returns(async () =>
        {
            await using var other = TestDb.Named(db);
            await new ExecutionStateService(other, new Mock<IHubContext<ExecutionHub>>().Object)
                .SetFlagsAsync(execution.Id, new SetFlagsDto(["coffee-ready"], null, "white-cell"), default);
            return true;
        });

        await Dispatcher(context).DispatchDueAsync(DateTime.UtcNow, default);

        await using var check = TestDb.Named(db);
        var state = await new ExecutionStateService(check, new Mock<IHubContext<ExecutionHub>>().Object).GetAsync(execution.Id, default);
        Assert.Contains("coffee-ready", state.Flags);
        Assert.True(Assert.Single(state.Objectives).Met);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private ExecutionDispatcher Dispatcher(ApplicationDbContext context) =>
        new(context, _clientHub.Object, new Mock<IHubContext<ExecutionHub>>().Object);

    private static ScenarioTimelineEvent Item(int number, string description, string? extras = null) => new()
    {
        Number = number, Time = "T+0m", Assigned = "Green Cell", Description = description, ObjectiveIds = "[]",
        ExecutionType = ExecutionType.Npc, Extras = extras
    };

    private async Task<Execution> Running(ApplicationDbContext context, params ScenarioTimelineEvent[] events)
    {
        var scenario = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, Extras = Terrain };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var timeline = new ScenarioTimeline { ScenarioId = scenario.Id };
        foreach (var e in events) timeline.ScenarioTimelineEvents.Add(e);
        context.ScenarioTimelines.Add(timeline);
        await context.SaveChangesAsync();
        var service = new ExecutionService(context, _clientHub.Object, new Mock<IHubContext<ExecutionHub>>().Object);
        var execution = await service.CreateAsync(new CreateExecutionDto(scenario.Id, "office-r1", null, null, null), default);
        await service.StartAsync(execution.Id, default);
        return context.Executions.Single(e => e.Id == execution.Id);
    }

    private static Guid Npc(ApplicationDbContext context, int executionId, string team, string host)
    {
        var npc = new NpcRecord { Id = Guid.NewGuid(), ExecutionId = executionId, Team = team, HostName = host, NpcProfile = new NpcProfile(), CreatedUtc = DateTime.UtcNow };
        context.Npcs.Add(npc);
        context.SaveChanges();
        return npc.Id;
    }

    private static Guid Machine(ApplicationDbContext context, string name)
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = name, FQDN = $"{name}.office.local", Host = name, ResolvedHost = name };
        context.Machines.Add(machine);
        context.SaveChanges();
        return machine.Id;
    }

    private static ExecutionTimelineItem ItemOf(ApplicationDbContext context, int executionId, int number) =>
        context.ExecutionTimelineItems.AsNoTracking().Single(i => i.ExecutionId == executionId && i.Number == number);

    private static void ResetItem(ApplicationDbContext context, int executionId, int number)
    {
        var item = context.ExecutionTimelineItems.Single(i => i.ExecutionId == executionId && i.Number == number);
        item.Status = "Pending";
        context.SaveChanges();
    }
}
