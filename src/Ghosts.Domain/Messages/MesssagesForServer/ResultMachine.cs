// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ghosts.Domain
{
    public class ResultMachine
    {
        public ResultMachine()
        {
            Name = Environment.MachineName.ToLower();
            FQDN = GetHost();
            Domain = GetDomain();
            Host = Dns.GetHostName().ToLower();
            ResolvedHost = GetResolvedHost();
            ClientIp = GetLocalIPAddress();
            //this.IpAddress would be only set by API server picking up the request
            CurrentUsername = Environment.UserName;
        }

        public ResultMachine(string name, string fqdn, string domain, string host, string resolvedHost, string clientIp, string incomingIp,
            string username)
        {
            Name = name.ToLower();
            FQDN = fqdn.ToLower();
            Domain = domain;
            Host = host.ToLower();
            ResolvedHost = resolvedHost.ToLower();
            ClientIp = clientIp;
            IpAddress = incomingIp;
            CurrentUsername = username;
        }

        public string Id { get; set; }
        public string Name { get; private set; }
        public string FQDN { get; }
        public string Domain { get; }
        public string Host { get; }
        public string ResolvedHost { get; }
        public string ClientIp { get; }
        public string IpAddress { get; }
        public string CurrentUsername { get; }

        public override string ToString()
        {
            return
                $"Name:{Name}|FQDN:{FQDN}|Domain:{Domain}|Host:{Host}|ResolvedHost:{ResolvedHost}|HostIP:{ClientIp}|IP:{IpAddress}|User:{CurrentUsername}";
        }

        public void SetName(string name)
        {
            if (!string.IsNullOrEmpty(name))
                name = name.ToLower();

            Name = name;
        }

        private static string GetHost()
        {
            try
            {
                return Dns.GetHostEntry("localhost").HostName.ToLower();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetResolvedHost()
        {
            try
            {
                return Dns.GetHostEntry("localhost").HostName.ToLower();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetDomain()
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties().DomainName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The address the client is reachable on. Deliberately not resolved from the hostname:
        /// Linux hosts commonly map their own name to a loopback address in /etc/hosts (the
        /// Debian/Ubuntu default is 127.0.1.1) or do not resolve it at all, so a hostname lookup
        /// reports loopback or nothing instead of the real address.
        /// </summary>
        private static string GetLocalIPAddress()
        {
            return GetRoutedAddress() ?? GetInterfaceAddress() ?? "-9";
        }

        private static string GetRoutedAddress()
        {
            try
            {
                // connecting a datagram socket sends no traffic, it only asks the OS which local
                // address it would route from - the one the API sees the client arrive on
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var address = (socket.LocalEndPoint as IPEndPoint)?.Address;
                    return IsReportable(address) ? address.ToString() : null;
                }
            }
            catch
            {
                // no route off this host, fall back to the interface list
                return null;
            }
        }

        private static string GetInterfaceAddress()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                                x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(x => x.GetIPProperties())
                    // an interface with a gateway is the one carrying real traffic - virtual
                    // bridges such as docker0 and virbr0 generally have none
                    .OrderByDescending(x => x.GatewayAddresses.Any(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                    .SelectMany(x => x.UnicastAddresses)
                    .Select(x => x.Address)
                    .FirstOrDefault(IsReportable)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsReportable(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                return false;

            // a link-local (failed DHCP) address tells the API nothing about how to reach the client
            var bytes = address.GetAddressBytes();
            return !(bytes[0] == 169 && bytes[1] == 254);
        }
    }
}
