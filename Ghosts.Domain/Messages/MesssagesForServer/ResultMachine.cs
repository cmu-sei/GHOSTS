// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ghosts.Domain
{
    public class ResultMachine
    {
        public string Id { get; set; }
        public string Name { get; private set; }
        public string FQDN { get; private set; }
        public string Domain { get; private set; }
        public string Host { get; private set; }
        public string ResolvedHost { get; private set; }
        public string ClientIp { get; private set; }
        public string IpAddress { get; private set; }
        public string CurrentUsername { get; private set; }

        public ResultMachine()
        {
            Name = Environment.MachineName;
            FQDN = GetHost();
            Domain = GetDomain();
            Host = Dns.GetHostName();
            ResolvedHost = GetResolvedHost();
            ClientIp = GetLocalIPAddress();
            //this.IpAddress would be only set by API server picking up the request
            CurrentUsername = Environment.UserName;
        }

        public ResultMachine(string name, string fqdn, string domain, string host, string resolvedHost, string clientIp, string incomingIp, string username)
        {
            Name = name;
            FQDN = fqdn;
            Domain = domain;
            Host = host;
            ResolvedHost = resolvedHost;
            ClientIp = clientIp;
            IpAddress = incomingIp;
            CurrentUsername = username;
        }

        public override string ToString()
        {
            return $"Name:{Name}|FQDN:{FQDN}|Domain:{Domain}|Host:{Host}|ResolvedHost:{ResolvedHost}|HostIP:{ClientIp}|IP:{IpAddress}|User:{CurrentUsername}";
        }

        public void SetName(string name)
        {
            Name = name;
        }

        private static string GetHost()
        {
            try
            {
                return Dns.GetHostEntry("localhost").HostName;
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
                return Dns.GetHostEntry("localhost").HostName;
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
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            return "-9";
        }
    }
}
