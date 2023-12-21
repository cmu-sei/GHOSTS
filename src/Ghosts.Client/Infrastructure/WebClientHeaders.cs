// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Web.UI;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Infrastructure;

/// <summary>
/// Sets web request headers for updates/post of results
/// </summary>
public static class WebClientBuilder
{
    public static WebClient Build(ResultMachine machine, bool useId = true)
    {
        var client = new WebClient();
        client.Headers.Add(HttpRequestHeader.UserAgent, "Ghosts Client");
        if (Program.CheckId != null && !string.IsNullOrEmpty(Program.CheckId.Id) && useId)
        {
            client.Headers.Add("ghosts-id", Program.CheckId.Id);
        }
        client.Headers.Add("ghosts-name", machine.Name);
        client.Headers.Add("ghosts-fqdn", machine.FQDN);
        client.Headers.Add("ghosts-host", machine.Host);
        client.Headers.Add("ghosts-domain", machine.Domain);
        client.Headers.Add("ghosts-resolvedhost", machine.ResolvedHost);
        client.Headers.Add("ghosts-ip", machine.ClientIp);

        var username = machine.CurrentUsername;
        if (Program.Configuration.EncodeHeaders)
            username = Base64Encoder.Base64Encode(username);

        client.Headers.Add("ghosts-user", username);
        client.Headers.Add("ghosts-version", ApplicationDetails.Version);
        return client;
    }
}