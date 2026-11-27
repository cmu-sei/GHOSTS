// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ghosts.Api.Infrastructure.Models;
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

        public static IEnumerable<string> GetGroupNames(Machine machine) =>
            GetGroupNames(machine, Program.ApplicationSettings.Grouping);

        public static IEnumerable<string> GetGroupNames(Machine machine, ApplicationSettings.GroupingOptions grouping)
        {
            var list = new List<string>();
            if (grouping is not { Enabled: true }) return list;

            try
            {
                var groupNameFormat = grouping.GroupName;
                var delimeters = grouping.GroupDelimiters;

                var host = machine.Host;
                var domain = machine.Domain;
                var resolvedHost = machine.ResolvedHost;
                var fqdn = machine.FQDN;
                var name = machine.Name;

                foreach (var d in grouping.GroupingDefinition)
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

                // Drop empty parts: a machine with no domain would otherwise get "-host-*" names and a catch-all "-*"
                var nameParts = groupNameFormat.Split(delimeters.ToArray(), StringSplitOptions.RemoveEmptyEntries);

                for (var i = nameParts.GetUpperBound(0); i > 0; i--)
                {
                    var g = new StringBuilder();
                    for (var j = 0; j < i; j++)
                        g.Append(nameParts[j]).Append(delimeters[0]);

                    list.Add(g.Append('*').ToString().TrimEnd(Convert.ToChar(delimeters[0])));

                    if (list.Count > grouping.GroupDepth)
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
