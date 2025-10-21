// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Api.Infrastructure.Models;

public class NpcInteraction
{
    public int Id { get; set; }
    public int SocialConnectionId { get; set; }
    public long Step { get; set; }
    public int Value { get; set; }

    // Navigation property
    public NpcSocialConnection SocialConnection { get; set; }
}
