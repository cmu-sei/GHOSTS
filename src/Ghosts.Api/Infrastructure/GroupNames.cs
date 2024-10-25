// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ghosts.api.Infrastructure.Models;
using NLog;

namespace Ghosts.Api.Infrastructure
{
    public static class GroupNames
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private static string FormatToken(List<char> delimeters, ApplicationSettings.GroupingOptions.GroupingDefinitionOption d, string o)
        {
            // replace
            foreach (var (key, value) in d.Replacements) o = o.Replace(key, value);

            // reverse?
            if (d.Direction.Equals("RightToLeft")) o = o.Split(delimeters.ToArray()).Reverse().ToString();

            return o;
        }

        public static IEnumerable<string> GetGroupNames(Machine machine)
        {
            var list = new List<string>();

            try
            {
                var groupNameFormat = Program.ApplicationSettings.Grouping.GroupName;
                var delimeters = Program.ApplicationSettings.Grouping.GroupDelimiters;

                var host = machine.Host;
                var domain = machine.Domain;
                var resolvedHost = machine.ResolvedHost;
                var fqdn = machine.FQDN;
                var name = machine.Name;

                foreach (var d in Program.ApplicationSettings.Grouping.GroupingDefinition)
                    switch (d.Value)
                    {
                        case "host":
                            host = FormatToken(delimeters, d, host);
                            break;
                        case "domain":
                            domain = FormatToken(delimeters, d, domain);
                            break;
                        case "resolvedhost":
                            resolvedHost = FormatToken(delimeters, d, resolvedHost);
                            break;
                        case "name":
                            name = FormatToken(delimeters, d, name);
                            break;
                        case "fqdn":
                            fqdn = FormatToken(delimeters, d, fqdn);
                            break;
                    }

                groupNameFormat = groupNameFormat.Replace("{host}", host);
                groupNameFormat = groupNameFormat.Replace("{domain}", domain);
                groupNameFormat = groupNameFormat.Replace("{resolvedhost}", resolvedHost);
                groupNameFormat = groupNameFormat.Replace("{name}", name);
                groupNameFormat = groupNameFormat.Replace("{fqdn}", fqdn);

                var nameParts = groupNameFormat.Split(delimeters.ToArray());

                for (var i = nameParts.GetUpperBound(0); i > 0; i--)
                {
                    var g = new StringBuilder();
                    for (var j = 0; j < i; j++)
                        g.Append(nameParts[j]).Append(delimeters[0]);

                    list.Add(g.Append('*').ToString().TrimEnd(Convert.ToChar(delimeters[0])));

                    if (list.Count > Program.ApplicationSettings.Grouping.GroupDepth)
                    {
                        // groups deeper than 3 become a performance issue
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }

            return list;
        }
    }
}
