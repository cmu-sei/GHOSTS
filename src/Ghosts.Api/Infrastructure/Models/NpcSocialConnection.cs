// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcSocialConnection
{
    public string Id { get; set; }
    public Guid SocialGraphId { get; set; }
    public Guid ConnectedNpcId { get; set; }
    public string Name { get; set; }
    public string Distance { get; set; }
    public int RelationshipStatus { get; set; }

    // Navigation properties
    public NpcSocialGraph SocialGraph { get; set; }
    public ICollection<NpcInteraction> Interactions { get; set; }

    public NpcSocialConnection()
    {
        Id = Guid.NewGuid().ToString();
        Interactions = new List<NpcInteraction>();
    }
}
