// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcLearning
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }
    public Guid SocialGraphId { get; set; }
    public Guid ToNpcId { get; set; }
    public Guid FromNpcId { get; set; }
    public string Topic { get; set; }
    public long Step { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Value { get; set; }

    // Navigation property
    public NpcSocialGraph SocialGraph { get; set; }

    public NpcLearning() { }

    public NpcLearning(Guid socialGraphId, Guid toNpcId, Guid fromNpcId, string topic, long step, int value)
    {
        SocialGraphId = socialGraphId;
        ToNpcId = toNpcId;
        FromNpcId = fromNpcId;
        Topic = topic;
        Step = step;
        Value = value;
    }

    public override string ToString()
    {
        return $"{ToNpcId},{FromNpcId},{Topic},{Step},{Value}";
    }
}
