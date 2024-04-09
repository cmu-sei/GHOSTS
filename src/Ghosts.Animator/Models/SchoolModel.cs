// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class School
    {
        public string Name { get; set; }
        public string Location { get; set; }
    }

    public class UniversityType
    {
        public string Type { get; set; }
        public IList<School> Schools { get; set; }
    }

    public class UniversityManager
    {
        public IList<UniversityType> UniversityTypes { get; set; }
    }
}
