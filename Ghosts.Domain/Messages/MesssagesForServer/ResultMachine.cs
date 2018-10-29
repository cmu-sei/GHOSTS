// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net;
using System.Net.Sockets;

namespace Ghosts.Domain
{
    public class ResultMachine
    {
        public string Id { get; set; }
        public string Name { get; private set; }
        public string FQDN { get; private set; }
        public string ClientIp { get; private set; }
        public string IpAddress { get; private set; }
        public string CurrentUsername { get; private set; }

        public ResultMachine()
        {
            this.Name = Environment.MachineName;
            this.FQDN = Dns.GetHostName();
            this.ClientIp = GetLocalIPAddress();
            //this.IpAddress would be only set by API server picking up the request
            this.CurrentUsername = Environment.UserName;
        }

        public ResultMachine(string name, string fqdn, string clientIp, string incomingIp, string username)
        {
            this.Name = name;
            this.FQDN = fqdn;
            this.ClientIp = clientIp;
            this.IpAddress = incomingIp;
            this.CurrentUsername = username;
        }

        public override string ToString()
        {
            return $"Name:{this.Name}|FQDN:{this.FQDN}|HostIP:{this.ClientIp}|IP:{this.IpAddress}|User:{this.CurrentUsername}";
        }

        private static string GetLocalIPAddress()
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
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
