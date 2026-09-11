// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Ghosts.Domain;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Infrastructure;

/// <summary>
/// ClientIp is sent to the API as the ghosts-ip header (see HttpClientBuilder) and is stored and
/// displayed as the machine's HostIp, so it has to be the address the client is actually reachable
/// on. On Linux the hostname is commonly mapped to a loopback address in /etc/hosts (127.0.1.1 is
/// the Debian/Ubuntu default) or is not resolvable at all, which is why it must not be derived from
/// a hostname lookup.
/// </summary>
public class ResultMachineTests
{
    [Fact]
    public void ClientIp_IsNotLoopback()
    {
        var machine = new ResultMachine();

        Assert.True(IPAddress.TryParse(machine.ClientIp, out var address), $"ClientIp was '{machine.ClientIp}'");
        Assert.False(IPAddress.IsLoopback(address), $"ClientIp was loopback: {machine.ClientIp}");
    }

    [Fact]
    public void ClientIp_IsRoutableIpv4()
    {
        var machine = new ResultMachine();

        var address = IPAddress.Parse(machine.ClientIp);
        Assert.Equal(AddressFamily.InterNetwork, address.AddressFamily);
        Assert.NotEqual(169, address.GetAddressBytes()[0]); // link-local, i.e. DHCP never answered
    }

    [Fact]
    public void ClientIp_BelongsToAnUpNonLoopbackInterface()
    {
        var machine = new ResultMachine();

        var interfaceAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                        x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Select(x => x.Address.ToString())
            .ToList();

        Assert.Contains(machine.ClientIp, interfaceAddresses);
    }

    [Fact]
    public void ClientIp_DoesNotDependOnHostnameResolution()
    {
        var machine = new ResultMachine();

        // the address reported is the one the OS routes from, so a hostname that resolves to
        // loopback - or does not resolve at all - cannot be what gets sent to the API
        var resolved = ResolveHostname();
        if (resolved != null && IPAddress.IsLoopback(resolved))
        {
            Assert.NotEqual(resolved.ToString(), machine.ClientIp);
        }

        Assert.NotEqual("-9", machine.ClientIp);
    }

    private static IPAddress ResolveHostname()
    {
        try
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
            return null;
        }
    }
}
