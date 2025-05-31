// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Net.Http;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure
{
    /// <summary>
    /// Sets web request headers for updates/post of results
    /// </summary>
    public static class HttpClientBuilder
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static HttpClient Build(ResultMachine machine, bool useId = true)
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };

            var client = new HttpClient(handler);
            foreach (var header in GetHeaders(machine, useId))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            return client;
        }

        public static IDictionary<string, string> GetHeaders(ResultMachine machine, bool useId = true)
        {
            var dict = new Dictionary<string, string>
            {
                { "User-Agent", "Ghosts Client" },
                { "ghosts-name", machine.Name },
                { "ghosts-fqdn", machine.FQDN },
                { "ghosts-host", machine.Host },
                { "ghosts-domain", machine.Domain },
                { "ghosts-resolvedhost", machine.ResolvedHost },
                { "ghosts-ip", machine.ClientIp },
                { "ghosts-version", ApplicationDetails.VersionFile }
            };

            if (useId && Program.CheckId != null && !string.IsNullOrEmpty(Program.CheckId.Id))
            {
                dict.Add("ghosts-id", Program.CheckId.Id);
            }

            var username = machine.CurrentUsername;
            if (Program.Configuration.EncodeHeaders)
                username = Base64Encoder.Base64Encode(username);

            dict.Add("ghosts-user", username);

            _log.Trace($"Http request headers generated: {JsonConvert.SerializeObject(dict)}");

            return dict;
        }
    }
}
