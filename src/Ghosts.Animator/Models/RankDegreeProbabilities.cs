// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class RankDegreeProbability
    {
        public string Rank { get; set; }
        public double AssociatesProbability { get; set; }
        public double BachelorsProbability { get; set; }
        public double MastersProbability { get; set; }
        public double DoctorateProbability { get; set; }
        public double ProfessionalProbability { get; set; }
    }

    public class RankDegreeProbabilityManager
    {
        public IList<RankDegreeProbability> RankDegreeProbabilities { get; set; }
    }
}
