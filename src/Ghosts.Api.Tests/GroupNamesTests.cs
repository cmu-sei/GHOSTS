// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Models;
using Xunit;

namespace Ghosts.Api.Tests;

public class GroupNamesTests
{
    // The shipped appsettings.json grouping
    private static ApplicationSettings.GroupingOptions Grouping(bool enabled = true) => new()
    {
        Enabled = enabled,
        GroupDepth = 3,
        GroupDelimiters = new List<char> { '-' },
        GroupName = "{domain}-{host}",
        GroupingDefinition = new[] { "domain", "host" }.Select(v => new ApplicationSettings.GroupingOptions.GroupingDefinitionOption
        {
            Value = v,
            Direction = "LeftToRight",
            Replacements = new Dictionary<string, string> { { ".", "-" }, { "^", "-" } }
        }).ToList()
    };

    [Fact]
    public void DomainMachine_GetsPrefixGroups()
    {
        var names = GroupNames.GetGroupNames(new Machine { Host = "jarn-wks-03", Domain = "jarn.gov.au" }, Grouping());

        Assert.Equal(new[] { "jarn-gov-au-jarn-wks-*", "jarn-gov-au-jarn-*", "jarn-gov-au-*", "jarn-gov-*" }, names);
    }

    [Fact]
    public void MachineWithoutDomain_GetsNoLeadingDelimiterOrCatchAll()
    {
        var names = GroupNames.GetGroupNames(new Machine { Host = "jarn-dev-wks-01", Domain = "" }, Grouping());

        Assert.Equal(new[] { "jarn-dev-wks-*", "jarn-dev-*", "jarn-*" }, names);
    }

    [Fact]
    public void Disabled_GetsNoGroups()
    {
        Assert.Empty(GroupNames.GetGroupNames(new Machine { Host = "jarn-wks-03", Domain = "jarn.gov.au" }, Grouping(enabled: false)));
    }
}
