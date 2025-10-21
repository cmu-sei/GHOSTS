// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Newtonsoft.Json;

namespace Ghosts.Api.Infrastructure.Models;

[method: JsonConstructor]
public class NpcBelief(int id, Guid socialGraphId, Guid toNpcId, Guid fromNpcId, string name, long step, decimal likelihood, decimal posterior)
{
    public int Id { get; set; } = id;
    public Guid SocialGraphId { get; set; } = socialGraphId;
    public Guid ToNpcId { get; set; } = toNpcId;
    public Guid FromNpcId { get; set; } = fromNpcId;
    public string Name { get; set; } = name;
    public long Step { get; set; } = step;
    public decimal Likelihood { get; set; } = likelihood;
    public decimal Posterior { get; set; } = posterior;

    // Navigation property
    public NpcSocialGraph SocialGraph { get; set; }

    public NpcBelief() : this(0, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, 0, 0, 0) { }

    public override string ToString()
    {
        return $"{ToNpcId},{FromNpcId},{Name},{Step},{Likelihood},{Posterior}";
    }

    public static string ToHeader()
    {
        return "To,From,Name,Step,Likelihood,Posterior";
    }
}
