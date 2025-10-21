// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcSocialGraph
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public long CurrentStep { get; set; }

    // Navigation properties
    public ICollection<NpcSocialConnection> Connections { get; set; }
    public ICollection<NpcLearning> Knowledge { get; set; }
    public ICollection<NpcBelief> Beliefs { get; set; }

    public NpcSocialGraph()
    {
        Connections = new List<NpcSocialConnection>();
        Knowledge = new List<NpcLearning>();
        Beliefs = new List<NpcBelief>();
    }
}
