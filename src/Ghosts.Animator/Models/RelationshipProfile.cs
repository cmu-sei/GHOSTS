// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Animator.Models
{
    public class RelationshipProfile
    {
        public int Id { get; set; }
        public Guid With { get; set; }
        public string Type { get; set; }
    }
}
