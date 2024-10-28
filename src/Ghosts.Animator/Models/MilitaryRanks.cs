// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Models
{
    public class MilitaryRank
    {
        public string Country { get; set; }
        public IList<Branch> Branches { get; set; }

        public class Branch
        {
            public string Name { get; set; }
            public IList<Rank> Ranks { get; set; }

            public class Rank
            {
                [JsonConverter(typeof(StringEnumConverter))]
                public MilitaryBranch Branch { get; set; }
                public string Pay { get; set; }
                public string Name { get; set; }
                public string Abbr { get; set; }
                public string Classification { get; set; }

                //Position number wrt this unit
                public string Billet { get; set; }
                public string MOS { get; set; }
                public string MOSID { get; set; }
                public double Probability { get; set; }
            }
        }
    }

    public class Billet
    {
        public string Pay { get; set; }
        public IList<string> Roles { get; set; }
    }

    public class Branch
    {
        public string Name { get; set; }
        public IList<Billet> Billets { get; set; }
    }

    public class BilletManager
    {
        public string Country { get; set; }
        public IList<Branch> Branches { get; set; }
    }

    public class MOSModels
    {
        public class Item
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string High { get; set; }
            public string Low { get; set; }
        }

        public class MO
        {
            public string Type { get; set; }
            public IList<Item> Items { get; set; }
            public string Low { get; set; }
        }

        public class Branch
        {
            public string Name { get; set; }
            public IList<MO> MOS { get; set; }
            public string Url { get; set; }
        }

        public class MOSManager
        {
            public string Country { get; set; }
            public IList<Branch> Branches { get; set; }
        }
    }
}
