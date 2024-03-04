// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    class MilitaryHeightWeight
    {
        public class Heights
        {
            public int Height { get; set; }
            public int MinWeight { get; set; }
            public int MaxWeight { get; set; }
        }

        public class Ages
        {
            public int Age { get; set; }
            public IList<Heights> Heights { get; set; } //ARMY Heights list found here, instead of in Sexes
        }

        public class Sexes
        {
            public string Sex { get; set; } //Will be Null for AF and CG
            public IList<Heights> Heights { get; set; } //Will be Null for ARMY (I think, warrants testing)
            public IList<Ages> Ages { get; set; } //Will be Null for all branches except ARMY
        }

        public class Branches
        {
            public string Branch { get; set; }
            public IList<Heights> Heights { get; set; }
            public IList<Sexes> Sexes { get; set; } //Will be Null for AF and CG
        }

        public class MilitaryHeightWeightManager
        {
            public IList<Branches> Branches { get; set; }
        }
    }
}
