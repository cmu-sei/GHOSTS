// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using Ghosts.Client.Comms;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Code
{
    /// <summary>
    /// Sets web request headers for updates/post of results
    /// </summary>
    public static class WebClientBuilder
    {
        public static WebClient Build(ResultMachine machine)
        {
            return BuildEx(machine);
        }

        public static WebClient BuildNoId(ResultMachine machine)
        {
            return BuildEx(machine, false);
        }

        private static WebClient BuildEx(ResultMachine machine, bool hasId = true)
        {
            var client = new WebClient();
            client.Headers.Add(HttpRequestHeader.UserAgent, "Ghosts Client");
            if (hasId)
            {
                client.Headers.Add("id", CheckId.Id);
            }
            client.Headers.Add("name", machine.Name);
            client.Headers.Add("fqdn", machine.FQDN);
            client.Headers.Add("ip", machine.ClientIp);
            client.Headers.Add("user", machine.CurrentUsername);
            client.Headers.Add("version", ApplicationDetails.Version);
            return client;
        }

    }
}
