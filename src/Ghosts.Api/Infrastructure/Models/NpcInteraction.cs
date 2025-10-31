// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcInteraction
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }
    public string SocialConnectionId { get; set; }
    public long Step { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Value { get; set; }

    // Navigation property
    public NpcSocialConnection SocialConnection { get; set; }
}
