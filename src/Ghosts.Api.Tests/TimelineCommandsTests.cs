// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Animator.Models;
using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// Raw terminal commands sent to a machine are wrapped in a one-shot partial timeline the client already runs:
/// pushed over the socket when the client is connected, otherwise queued for its next poll.
/// </summary>
public class TimelineCommandsTests
{
    private readonly Mock<IClientHubService> _clientHub = new();

    [Fact]
    public async Task SendCommands_PushesOneShotTimelineOverSocket_WhenClientConnected()
    {
        await using var context = NewContext();
        var machineId = AddMachine(context);
        MachineUpdate? sent = null;
        _clientHub.Setup(h => h.SendUpdate(machineId, It.IsAny<MachineUpdate>()))
            .Callback<Guid, MachineUpdate>((_, u) => sent = u)
            .ReturnsAsync(true);

        var result = await NewService(context).SendCommandsAsync(machineId, HandlerType.Bash, ["whoami", "ls /tmp"], CancellationToken.None);

        Assert.True(result.Found);
        Assert.True(result.Pushed);
        Assert.Empty(context.MachineUpdates);
        Assert.NotNull(sent);
        Assert.Equal(UpdateClientConfig.UpdateType.TimelinePartial, sent.Type);
        Assert.Equal(result.TimelineId, sent.Update.Id);
        Assert.NotEqual(Guid.Empty, result.TimelineId);
        var handler = Assert.Single(sent.Update.TimeLineHandlers);
        Assert.Equal(HandlerType.Bash, handler.HandlerType);
        Assert.False(handler.Loop);
        Assert.Equal(["whoami", "ls /tmp"], handler.TimeLineEvents.Select(e => e.Command));
    }

    [Fact]
    public async Task SendCommands_QueuesMachineUpdate_WhenClientNotConnected()
    {
        await using var context = NewContext();
        var machineId = AddMachine(context);
        _clientHub.Setup(h => h.SendUpdate(machineId, It.IsAny<MachineUpdate>())).ReturnsAsync(false);

        var result = await NewService(context).SendCommandsAsync(machineId, HandlerType.PowerShell, ["Get-Process"], CancellationToken.None);

        Assert.True(result.Found);
        Assert.False(result.Pushed);
        var queued = Assert.Single(context.MachineUpdates);
        Assert.Equal(machineId, queued.MachineId);
        Assert.Equal(StatusType.Active, queued.Status);
        Assert.Equal(UpdateClientConfig.UpdateType.TimelinePartial, queued.Type);
        Assert.Equal(result.TimelineId, queued.Update.Id);
        Assert.Equal(HandlerType.PowerShell, Assert.Single(queued.Update.TimeLineHandlers).HandlerType);
    }

    [Fact]
    public async Task SendCommands_SendsNothing_ForUnknownMachine()
    {
        await using var context = NewContext();

        var result = await NewService(context).SendCommandsAsync(Guid.NewGuid(), HandlerType.Bash, ["whoami"], CancellationToken.None);

        Assert.False(result.Found);
        Assert.Empty(context.MachineUpdates);
        _clientHub.Verify(h => h.SendUpdate(It.IsAny<Guid>(), It.IsAny<MachineUpdate>()), Times.Never);
    }

    [Fact]
    public async Task Controller_Returns400_ForHandlerThatIsNotAShell()
    {
        var service = new Mock<ITimelineService>();

        var result = await NewController(service).TimelinesSendCommands(Guid.NewGuid(),
            new MachineCommandsViewModel { Shell = HandlerType.BrowserChrome, Commands = ["whoami"] }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controller_Returns400_WhenBodyDoesNotBind()
    {
        // e.g. "shell": "sh" fails JSON binding, and without [ApiController] the action is still called with null
        var service = new Mock<ITimelineService>();

        var result = await NewController(service).TimelinesSendCommands(Guid.NewGuid(), null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controller_Returns400_WhenNoCommandHasText()
    {
        var service = new Mock<ITimelineService>();

        var result = await NewController(service).TimelinesSendCommands(Guid.NewGuid(),
            new MachineCommandsViewModel { Commands = ["", "   "] }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controller_Returns404_ForUnknownMachine()
    {
        var service = new Mock<ITimelineService>();
        service.Setup(s => s.SendCommandsAsync(It.IsAny<Guid>(), It.IsAny<HandlerType>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, Guid.Empty, false));

        var result = await NewController(service).TimelinesSendCommands(Guid.NewGuid(),
            new MachineCommandsViewModel { Commands = ["whoami"] }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Controller_DefaultsToBash_DropsBlankLines_AndReportsDelivery()
    {
        var machineId = Guid.NewGuid();
        var timelineId = Guid.NewGuid();
        var service = new Mock<ITimelineService>();
        service.Setup(s => s.SendCommandsAsync(machineId, HandlerType.Bash, It.Is<IList<string>>(c => c.SequenceEqual(new[] { "whoami" })), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, timelineId, false));

        var result = await NewController(service).TimelinesSendCommands(machineId,
            new MachineCommandsViewModel { Commands = ["whoami", " "] }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(ok.Value);
        Assert.Equal(timelineId, body.GetProperty("timelineId").GetGuid());
        Assert.Equal("queued", body.GetProperty("delivery").GetString());
    }

    private TimelineService NewService(ApplicationDbContext context) =>
        new(context, new Mock<IHubContext<ActivityHub>>().Object, _clientHub.Object);

    private static TimelinesController NewController(Mock<ITimelineService> service) =>
        new(service.Object, new Mock<IMachineTimelinesService>().Object);

    private static Guid AddMachine(ApplicationDbContext context)
    {
        var machine = new Machine { Id = Guid.NewGuid(), Name = "workstation-01" };
        context.Machines.Add(machine);
        context.SaveChanges();
        return machine.Id;
    }

    private static ApplicationDbContext NewContext() => new TestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"timeline-commands-{Guid.NewGuid()}")
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private class TestDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<NpcRecord>().Property(n => n.NpcProfile).HasConversion(
                profile => JsonSerializer.Serialize(profile, (JsonSerializerOptions?)null),
                text => JsonSerializer.Deserialize<NpcProfile>(text, (JsonSerializerOptions?)null)!);
        }
    }
}
