using System;
using Ghosts.Api.Infrastructure.Models;
using Xunit;

namespace Ghosts.Api.Tests;

public class MachineTests
{
    [Fact]
    public void UpdateFromCheckIn_UpdatesUsernameAndStatus()
    {
        var lastReported = DateTime.UtcNow.AddHours(-1);
        var machine = new Machine
        {
            Name = "workstation-01",
            FQDN = "workstation-01.example.test",
            Domain = "example.test",
            Host = "workstation-01",
            ResolvedHost = "workstation-01.example.test",
            HostIp = "192.0.2.10",
            CurrentUsername = "user-a",
            ClientVersion = "9.0.0",
            Status = StatusType.Deleted,
            StatusUp = Machine.UpDownStatus.Down,
            LastReportedUtc = lastReported
        };

        machine.UpdateFromCheckIn(new Machine
        {
            Name = "workstation-01",
            FQDN = "workstation-01.example.test",
            Domain = "example.test",
            Host = "workstation-01",
            ResolvedHost = "workstation-01.example.test",
            HostIp = "192.0.2.10",
            IPAddress = "::ffff:192.0.2.10",
            CurrentUsername = "user-b",
            ClientVersion = "9.3.0",
            StatusUp = Machine.UpDownStatus.Up
        });

        Assert.Equal("user-b", machine.CurrentUsername);
        Assert.Equal("9.3.0", machine.ClientVersion);
        Assert.Equal(StatusType.Active, machine.Status);
        Assert.Equal(Machine.UpDownStatus.Up, machine.StatusUp);
        Assert.Equal("::ffff:192.0.2.10", machine.IPAddress);
        Assert.True(machine.LastReportedUtc > lastReported);
    }
}
