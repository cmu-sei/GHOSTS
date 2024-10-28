// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using ghosts.api.Infrastructure.Models;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Models;
using Swashbuckle.AspNetCore.Filters;

namespace ghosts.api.Areas.Animator.Infrastructure.Models;

public class InsiderThreatGenerationConfiguration
{
    // A campaign is the top level of an engagement
    public string Campaign { get; set; }

    // Enclaves are specific subnets of a range, (or a larger number of people) 
    public IList<EnclaveConfiguration> Enclaves { get; set; }
}

public class InsiderThreatGenerationConfigurationExample : IExamplesProvider<InsiderThreatGenerationConfiguration>
{
    public InsiderThreatGenerationConfiguration GetExamples()
    {
        return new InsiderThreatGenerationConfiguration
        {
            Campaign = $"Exercise Season {DateTime.Now.Year}",
            Enclaves = new List<EnclaveConfiguration>
            {
                new()
                {
                    Name = $"Brigade {Faker.Company.Name()}",
                    Teams = new List<TeamConfiguration>
                    {
                        new()
                        {
                            Name = $"Engineering", DomainTemplate = "eng{machine_number}-brigade.unit.co",
                            MachineNameTemplate = "eng{machine_number}",
                            Npcs = new NpcConfiguration
                            {
                                Number = 10,
                                Configuration = new NpcGenerationConfiguration
                                    {Branch = MilitaryBranch.USARMY, Unit = "", RankDistribution = new List<RankDistribution>()}
                            }
                        }
                    }
                }
            }
        };
    }
}
