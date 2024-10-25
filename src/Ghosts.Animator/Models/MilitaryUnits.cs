// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class MilitaryUnit
    {
        public string Country { get; set; }
        public AddressProfiles.AddressProfile Address { get; set; }
        public IEnumerable<Unit> Sub { get; set; }

        public MilitaryUnit Clone()
        {
            return (MilitaryUnit)MemberwiseClone();
        }

        public class Unit
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Nick { get; set; }
            public string HQ { get; set; }
            public IEnumerable<Unit> Sub { get; set; }

            public Unit Clone()
            {
                return (Unit)MemberwiseClone();
            }
        }
    }
}
