// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class ForeignTravelProfile
    {
        public IEnumerable<Trip> Trips { get; set; }

        public ForeignTravelProfile()
        {
            Trips = new List<Trip>();
        }

        public class Trip
        {
            public string Code { get; set; }
            public string Country { get; set; }
            public string Destination { get; set; }
            public DateTime ArriveDestination { get; set; }
            public DateTime DepartDestination { get; set; }
        }
    }
}
