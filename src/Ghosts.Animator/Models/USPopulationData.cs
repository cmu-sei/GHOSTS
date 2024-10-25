// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class USPopulationData
    {
        public int TotalPopulation { get; set; }
        public IList<State> States { get; set; }

        public class State
        {
            public string Name { get; set; }
            public int Population { get; set; }
            public string Abbreviation { get; set; }
            public IList<City> Cities { get; set; }

            public class City
            {
                public string Name { get; set; }
                public string County { get; set; }
                public string Timezone { get; set; }
                public int Population { get; set; }
                public IList<ZipCode> ZipCodes { get; set; }

                public class ZipCode
                {
                    public string Id { get; set; }
                    public int Population { get; set; }
                }
            }
        }
    }
}
