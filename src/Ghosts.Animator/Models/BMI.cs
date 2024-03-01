// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class BMIs
    {
        public int BMI { get; set; }
        public int Weight { get; set; }
    }

    public class Heights
    {
        public int Height { get; set; }
        public IList<BMIs> BMIs { get; set; }
    }

    public class BMIManager
    {
        public IList<Heights> Heights { get; set; }
    }
}
