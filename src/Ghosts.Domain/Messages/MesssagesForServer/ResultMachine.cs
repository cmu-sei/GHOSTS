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

        private static string GetLocalIPAddress()
        {
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    return host.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)?.ToString();
                }
            }
            catch
            {
                // ignore
            }

            return "-9";
        }
    }
}
