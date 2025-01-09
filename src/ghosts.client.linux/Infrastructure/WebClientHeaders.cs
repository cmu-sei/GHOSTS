// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Net;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using NLog.Fluent;

namespace ghosts.client.linux.Infrastructure
{
    /// <summary>
    /// Sets web request headers for updates/post of results
    /// </summary>
    public static class WebClientBuilder
    {
        public static readonly Logger _log = LogManager.GetCurrentClassLogger();

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
            if (useId && Program.CheckId != null && !string.IsNullOrEmpty(Program.CheckId.Id))
            {
                dict.Add("ghosts-id", Program.CheckId.Id);
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
            dict.Add("ghosts-version", ApplicationDetails.Version);

            _log.Trace($"Webrequest headers generated: {JsonConvert.SerializeObject(dict)}");

            return dict;
        }
    }
}
