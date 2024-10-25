// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Models
{
    public class EducationProfile
    {
        public class Degree
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public DegreeLevel Level { get; set; }
            public string DegreeType { get; set; }

            public string Major { get; set; }

            //public string Minor;
            //public string Concentration;
            public School School { get; set; }

            public Degree() { }

            public Degree(DegreeLevel level, string major, School school)
            {
                Level = level;
                if (level == DegreeLevel.Bachelors || level == DegreeLevel.Associates
                                                   || level == DegreeLevel.Masters || level == DegreeLevel.Doctorate
                                                   || level == DegreeLevel.Professional)
                {
                    Major = major.Split(',')[0];
                    DegreeType = major.Split(',')[1];
                }
                else
                {
                    Major = major;
                    DegreeType = "";
                }

                School = school;
            }
        }

        public List<Degree> Degrees { get; set; }

        public override string ToString()
        {
            if (Degrees[0].Level == DegreeLevel.None)
            {
                return "Less than High School Education.";
            }

            if (Degrees[0].Level == DegreeLevel.GED)
            {
                return "GED";
            }

            if (Degrees[0].Level == DegreeLevel.HSDiploma)
            {
                return "High School Education.";
            }
            else
            {
                var o = "";
                foreach (var item in Degrees)
                {
                    o += $"{item.DegreeType} in {item.Major} from {item.School.Name}\n";
                }

                return o;
            }
        }
    }
}
