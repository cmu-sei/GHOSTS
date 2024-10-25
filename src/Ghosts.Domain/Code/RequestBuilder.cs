// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;

namespace Ghosts.Domain.Code
{
    public static class RequestBuilder
    {
        public static void Build(this RequestConfiguration requestConfiguration, TimelineHandler handler)
        {
            if (requestConfiguration.Uri == null)
            {
                return;
            }

            var url = requestConfiguration.Uri.ToString();

            // these are the standard replacements
            // {now} = short datetime
            url = Regex.Replace(url, "{now}", DateTime.Now.ToShortDateString());
            // /url/{uuid} = uuid
            url = Regex.Replace(url, "{uuid}", Guid.NewGuid().ToString());
            // {c} = character
            url = Regex.Replace(url, "{c}", "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNOPQRSTUVWXYZ".PickRandom().ToString());
            // {n} = number
            url = Regex.Replace(url, "{n}", new Random().Next(0, 1000).ToString());

            if (handler.HandlerArgs.ContainsKey("url-replace"))
            {
                var replacements = handler.HandlerArgs["url-replace"];
                foreach (var replacement in (JArray)replacements)
                {
                    foreach (var o in replacement)
                    {
                        url = Regex.Replace(url, "{" + ((JProperty)o).Name.ToString() + "}", ((Newtonsoft.Json.Linq.JArray)((JProperty)o).Value).PickRandom().ToString());
                    }
                }
            }

            requestConfiguration.Uri = new Uri(url);
        }
    }
}
