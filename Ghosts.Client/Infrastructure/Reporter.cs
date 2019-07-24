// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using Ghosts.Domain;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure
{
    public class Reporter
    {
        public static void Report(object payload, string uri)
        {
            var machine = new ResultMachine();
            using (var client = WebClientBuilder.Build(machine))
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.UploadString(uri, JsonConvert.SerializeObject(payload));
            }
        }
    }
}
