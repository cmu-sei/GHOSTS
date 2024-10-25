// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class MilitaryBases
    {
        public class Base
        {
            public string Name { get; set; }
            public IList<string> Streets { get; set; }
            public string City { get; set; }
            public string PostalCode { get; set; }
            public string State { get; set; }
        }

        public class BaseManager
        {
            public string Country { get; set; }
            public IList<Branch> Branches { get; set; }

            public class MilitaryBase
            {
                public string Name { get; set; }
                public IList<string> Streets { get; set; }
                public string City { get; set; }
                public string PostalCode { get; set; }
                public string State { get; set; }
            }

            public class Branch
            {
                public string Name { get; set; }
                public IList<MilitaryBase> Bases { get; set; }
            }
        }
    }
}
