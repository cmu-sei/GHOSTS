// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Animator.Enums;

namespace Ghosts.Animator.Models
{
    /// <summary>
    /// Pass this class into the npc generator for fine-tuned configuration,
    /// such as service branch, rank distribution, etc.
    /// </summary>
    public class NpcGenerationConfiguration
    {
        /// <summary>
        /// Set this if a specific service branch is needed
        /// </summary>
        public MilitaryBranch? Branch { get; set; }
        /// <summary>
        /// Set this to ensure specific ranks and their number are generated
        /// </summary>
        public IList<RankDistribution> RankDistribution { get; set; }
        /// <summary>
        /// Set this to generate NPCs that are all part of the same service branch unit
        /// </summary>
        public string Unit { get; set; }
    }

    public class RankDistribution
    {
        /// <summary>
        /// Corresponds to pay grade indications for each US service branch
        /// and their corresponding ranks 
        /// </summary>
        public string PayGrade { get; set; }
        /// <summary>
        /// The probability of generating this rank based on overall numbers provided
        /// </summary>
        public double Probability { get; set; }
        /// <summary>
        /// For larger distributions, set this to the number of minimum that must be generated for a scenario
        /// </summary>
        public int Minimum { get; set; }
    }
}