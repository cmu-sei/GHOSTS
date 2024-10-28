// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Lite.Infrastructure
{
    /// <summary>
    /// Sets web request headers for updates/post of results
    /// </summary>
    public static class WebClientBuilder
    {
        public static WebClient Build(ResultMachine machine, bool useId = true)
        {
            var client = new WebClient();
            foreach (var header in GetHeaders(machine, useId))
            {
                client.Headers.Add(header.Key, header.Value);
            }

            return client;
        }

        public static IDictionary<string, string> GetHeaders(ResultMachine machine, bool useId = true)
        {
            var dict = new Dictionary<string, string>();

            dict.Add(HttpRequestHeader.UserAgent.ToString(), "Ghosts Client");
            if (!string.IsNullOrEmpty(machine.Id))
            {
                dict.Add("ghosts-id", machine.Id);
            }

            dict.Add("ghosts-name", machine.Name);
            dict.Add("ghosts-fqdn", machine.FQDN);
            dict.Add("ghosts-host", machine.Host);
            dict.Add("ghosts-domain", machine.Domain);
            dict.Add("ghosts-resolvedhost", machine.ResolvedHost);
            dict.Add("ghosts-ip", machine.ClientIp);

            var username = machine.CurrentUsername;
            if (Program.Configuration.EncodeHeaders)
                username = Base64Encoder.Base64Encode(username);

            dict.Add("ghosts-user", username);
            dict.Add("ghosts-version", Domain.Code.ApplicationDetails.Version);

            return dict;
        }
    }
}
