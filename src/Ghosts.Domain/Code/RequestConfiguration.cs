// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ghosts.Domain.Code
{
    public class RequestConfiguration
    {
        public string GetHost()
        {
            return $"{Uri.Scheme}://{Uri.Host}";
        }

        /// <summary>
        /// URL to browse
        /// </summary>
        public Uri Uri { get; set; }
        /// <summary>
        /// For categorizing browsing, e.g. I want to simulate someone shopping for shoes on x, y and z sites
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public string Category { get; set; }
        /// <summary>
        /// GET, POST, PUT, DELETE
        /// </summary>
        public string Method { get; set; }
        // ReSharper disable once UnusedMember.Global
        public IDictionary<string, string> Headers { get; set; }
        public IDictionary<string, string> FormValues { get; set; }
        // ReSharper disable once UnusedMember.Global
        public string Body { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static RequestConfiguration Load(TimelineHandler handler, object o)
        {
            var commandArg = o.ToString();
            var result = new RequestConfiguration();
            if (commandArg != null && commandArg.StartsWith("{"))
            {
                result = JsonConvert.DeserializeObject<RequestConfiguration>(commandArg);
            }
            else
            {
                result.Method = "GET";
                if (Uri.TryCreate(commandArg, UriKind.Absolute, out var uri))
                {
                    result.Uri = uri;
                }
            }
            result.Build(handler);
            return result;
        }
    }
}
