// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Models;
using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The social site reports activity by username, so a username has to lead back to one NPC: the one whose
/// machine reports it, else the one with that email name, else the one named first.last.
/// </summary>
[Collection("Npc.Generate")]
public class NpcSocialHandleTests
{
    [Fact]
    public async Task The_machine_username_wins_over_the_email_name()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, email: "jsmith.mil@mail.mil", machineUsername: "jsmith");

        Assert.Equal(npc, (await Service(context).GetBySocialHandle("jsmith")).Id);
        Assert.Null(await Service(context).GetBySocialHandle("jsmith.mil"));
    }

    [Fact]
    public async Task Without_a_machine_username_the_email_name_is_the_handle()
    {
        await using var context = TestDb.New("npc-handle");
        var unbound = AddNpc(context, email: "amy.pond@example.test", first: "Amelia", last: "Pond");
        var idle = AddNpc(context, email: "rory@example.test", machineUsername: " ");

        Assert.Equal(unbound, (await Service(context).GetBySocialHandle("amy.pond")).Id);
        Assert.Null(await Service(context).GetBySocialHandle("amelia.pond"));
        Assert.Equal(idle, (await Service(context).GetBySocialHandle("rory")).Id);
    }

    [Fact]
    public async Task Without_an_email_the_handle_is_first_dot_last()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, first: "Mary Ann", last: "Summers");

        Assert.Equal(npc, (await Service(context).GetBySocialHandle("maryann.summers")).Id);
    }

    [Fact]
    public async Task Handles_match_whatever_the_case()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, machineUsername: "JSmith");

        Assert.Equal(npc, (await Service(context).GetBySocialHandle("jsmith")).Id);
        Assert.Equal(npc, (await Service(context).GetBySocialHandle(" JSMITH ")).Id);
    }

    [Fact]
    public async Task An_unknown_or_empty_username_finds_nothing()
    {
        await using var context = TestDb.New("npc-handle");
        AddNpc(context, email: "jsmith@example.test");

        Assert.Null(await Service(context).GetBySocialHandle("nobody"));
        Assert.Null(await Service(context).GetBySocialHandle(""));
        Assert.Null(await Service(context).GetBySocialHandle(null));
    }

    [Fact]
    public async Task A_shared_handle_goes_to_the_npc_whose_machine_reports_it_then_to_the_oldest()
    {
        await using var context = TestDb.New("npc-handle");
        var oldest = AddNpc(context, email: "jsmith@example.test", created: DateTime.UtcNow.AddDays(-2));
        AddNpc(context, email: "jsmith@example.test", created: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(oldest, (await Service(context).GetBySocialHandle("jsmith")).Id);

        var bound = AddNpc(context, machineUsername: "jsmith");

        Assert.Equal(bound, (await Service(context).GetBySocialHandle("jsmith")).Id);
    }

    [Fact]
    public async Task The_lookup_endpoint_returns_the_npc_or_not_found()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, machineUsername: "jsmith");
        var controller = new NpcsController(context, Service(context), new Mock<IHubContext<ActivityHub>>().Object,
            new Mock<IMachineUpdateService>().Object);

        var found = Assert.IsType<OkObjectResult>((await controller.NpcsLookup("jsmith")).Result);
        Assert.Equal(npc, Assert.IsType<NpcRecord>(found.Value).Id);
        Assert.IsType<NotFoundResult>((await controller.NpcsLookup("nobody")).Result);
    }

    [Fact]
    public async Task A_machine_gets_one_npc_even_when_it_registers_twice()
    {
        var database = $"npc-handle-{Guid.NewGuid()}";
        await using var context = TestDb.Named(database);
        var existing = AddNpc(context, first: "jsmith", machineUsername: "jsmith");
        var machines = new[] { "JSmith", "apond", "apond", "rwilliams" }
            .Select(u => new Machine { Id = Guid.NewGuid(), Name = "workstation-02", CurrentUsername = u }).ToList();
        context.Machines.AddRange(machines);
        context.SaveChanges();

        // Each check-in runs in its own request, as the HTTP and hub registrations of one client do
        await Task.WhenAll(machines.Select(m => Service(TestDb.Named(database)).CreateForMachine(m)));
        var service = Service(context);
        await service.SyncWithMachineUsernames();

        Assert.Equal(3, context.Npcs.Count());
        Assert.Equal(existing, (await service.GetBySocialHandle("jsmith")).Id);
        Assert.NotNull(await service.GetBySocialHandle("apond"));
        Assert.NotNull(await service.GetBySocialHandle("rwilliams"));
    }

    [Fact]
    public async Task The_same_post_reported_twice_is_stored_once()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, machineUsername: "jsmith");
        var service = Service(context);

        var first = await service.CreateActivity(npc, "SocialMediaPost", "Hello from chat");
        var again = await service.CreateActivity(npc, "SocialMediaPost", "Hello from chat");
        await service.CreateActivity(npc, "SocialMediaComment", "Hello from chat");
        await service.CreateActivity(npc, "SocialMediaPost", "Something else");

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(3, context.NpcActivities.Count());
    }

    [Fact]
    public async Task A_comment_reported_back_as_a_post_is_stored_once()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, machineUsername: "jsmith");
        var service = Service(context);

        var comment = await service.CreateActivity(npc, "SocialMediaComment", "Nice work");
        var reported = await service.CreateActivity(npc, "SocialMediaPost", "Nice work");

        Assert.Equal(comment.Id, reported.Id);
        Assert.Equal(1, context.NpcActivities.Count());
    }

    [Fact]
    public async Task Other_repeated_activity_is_stored_each_time()
    {
        await using var context = TestDb.New("npc-handle");
        var npc = AddNpc(context, machineUsername: "jsmith");
        var service = Service(context);

        await service.CreateActivity(npc, "NextAction", "Visited https://example.test");
        await service.CreateActivity(npc, "NextAction", "Visited https://example.test");
        await service.CreateActivity(npc, "SocialMediaComment", "Same comment");
        await service.CreateActivity(npc, "SocialMediaComment", "Same comment");

        Assert.Equal(4, context.NpcActivities.Count());
    }

    private static NpcService Service(ApplicationDbContext context) => new(context, new Mock<IEvidenceProcessor>().Object);

    private static Guid AddNpc(ApplicationDbContext context, string? email = null, string? first = null,
        string? last = null, string? machineUsername = null, DateTime? created = null)
    {
        Guid? machineId = null;
        if (machineUsername != null)
        {
            var machine = new Machine { Id = Guid.NewGuid(), Name = "workstation-01", CurrentUsername = machineUsername };
            context.Machines.Add(machine);
            machineId = machine.Id;
        }

        var npc = new NpcRecord
        {
            Id = Guid.NewGuid(),
            NpcProfile = new NpcProfile { Email = email, Name = new NameProfile { First = first, Last = last } },
            MachineId = machineId,
            CreatedUtc = created ?? DateTime.UtcNow
        };
        context.Npcs.Add(npc);
        context.SaveChanges();
        return npc.Id;
    }
}
