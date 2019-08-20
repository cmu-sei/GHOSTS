// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure.Browser
{
    public class RequestConfiguration
    {
        /// <summary>
        /// URL to browse
        /// </summary>
        public Uri Uri { get; set; }
        /// <summary>
        /// For categorizing browsing, e.g. I want to simulate someone shopping for shoes on x, y and z sites
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// GET, POST, PUT, DELETE
        /// </summary>
        public string Method { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public IDictionary<string, string> FormValues { get; set; }
        public string Body { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static RequestConfiguration Load(object o)
        {
            var commandArg = o.ToString();
            var result = new RequestConfiguration();
            if (commandArg.StartsWith("{"))
            {
                result = JsonConvert.DeserializeObject<RequestConfiguration>(commandArg);
                return result;
            }
            
            result.Method = "GET";
            if (Uri.TryCreate(commandArg, UriKind.Absolute, out var uri))
            {
                result.Uri = uri;
            }
        
            return result;
        }
    }
}
