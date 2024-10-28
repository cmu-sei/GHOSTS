// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class CareerProfile
    {
        public int WorkEthic { get; set; }
        public int TeamValue { get; set; }
        public IEnumerable<StrengthProfile> Strengths { get; set; }
        public IEnumerable<WeaknessProfile> Weaknesses { get; set; }

        public CareerProfile()
        {
            Strengths = new List<StrengthProfile>();
            Weaknesses = new List<WeaknessProfile>();
        }

        public class StrengthProfile
        {
            public string Name { get; set; }
        }

        public class WeaknessProfile
        {
            public string Name { get; set; }
        }
    }
}
