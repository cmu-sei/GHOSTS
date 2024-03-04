// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class Field
    {
        public string Name { get; set; }
        public double Percent { get; set; }
        public string DegreeType { get; set; }
        public IList<string> Majors { get; set; }
    }

    public class MajorDegreeLevel
    {
        public string Level { get; set; }
        public IList<Field> Fields { get; set; }
    }

    public class MajorManager
    {
        public IList<MajorDegreeLevel> MajorDegreeLevels { get; set; }
    }

}
