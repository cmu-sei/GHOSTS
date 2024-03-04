// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models.InsiderThreat
{
    public abstract class InsiderThreatBaseProfile
    {
        public int Id { get; set; }
        public List<RelatedEvent> RelatedEvents { get; set; }

        public InsiderThreatBaseProfile()
        {
            this.RelatedEvents = new List<RelatedEvent>();
        }
    }
}