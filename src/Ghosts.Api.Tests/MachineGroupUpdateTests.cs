// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Moq;
using Xunit;

namespace Ghosts.Api.Tests;

public class MachineGroupUpdateTests
{
    [Fact]
    public async Task UpdateGroupAsync_QueuesOneUpdatePerMachine()
    {
        await using var db = TestDb.New("group-update");
        var machines = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var group = new Group
        {
            Name = "workstations",
            GroupMachines = machines.Select(m => new GroupMachine { MachineId = m }).ToList()
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var service = new MachineUpdateService(db, new Mock<IServiceProvider>().Object, new Mock<IClientHubService>().Object);
        var found = await service.UpdateGroupAsync(group.Id, new MachineUpdateViewModel
        {
            Type = UpdateClientConfig.UpdateType.TimelinePartial,
            Update = new Timeline { Id = Guid.NewGuid(), TimeLineHandlers = new List<TimelineHandler>() }
        }, CancellationToken.None);

        Assert.True(found);
        Assert.Equal(machines.OrderBy(m => m), db.MachineUpdates.Select(u => u.MachineId).OrderBy(m => m).ToList());
    }

    [Fact]
    public async Task UpdateGroupAsync_ReturnsFalseForMissingGroup()
    {
        await using var db = TestDb.New("group-update-missing");
        var service = new MachineUpdateService(db, new Mock<IServiceProvider>().Object, new Mock<IClientHubService>().Object);

        Assert.False(await service.UpdateGroupAsync(42, new MachineUpdateViewModel(), CancellationToken.None));
        Assert.Empty(db.MachineUpdates);
    }
}
