// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace Ghosts.Animator.Models.InsiderThreat
{
    public class RelatedEvent
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string CorrectiveAction { get; set; }
        public string ReportedBy { get; set; }
        public DateTime Reported { get; set; }
    }
}
