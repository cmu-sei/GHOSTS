// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;

namespace Ghosts.Animator
{
    public static class MentalHealth
    {
        public static MentalHealthProfile GetMentalHealth()
        {
            var m = new MentalHealthProfile
            {
                HappyQuotient = AnimatorRandom.Rand.Next(1, 100),
                IQ = GetIQ(),
                MelancholyQuotient = AnimatorRandom.Rand.Next(1, 100),
                SpideySense = AnimatorRandom.Rand.Next(1, 100),
                SenseSomethingIsWrongQuotient = AnimatorRandom.Rand.Next(1, 100),
                AdherenceToPolicy = AnimatorRandom.Rand.Next(1, 100),
                EnthusiasmAndAttitude = AnimatorRandom.Rand.Next(1, 100),
                OpenToFeedback = AnimatorRandom.Rand.Next(1, 100),
                OverallPerformance = AnimatorRandom.Rand.Next(1, 100),
                GeneralPerformance = AnimatorRandom.Rand.Next(1, 100),
                InterpersonalSkills = AnimatorRandom.Rand.Next(1, 100)
            };
            return m;
        }

        public static int GetIQ()
        {
            return new Dictionary<string, double>
            {
                {"130|210", 2},
                {"121|130", 6},
                {"111|120", 16},
                {"90|110", 52},
                {"80|89", 15},
                {"70|79", 7}
            }.RandomFromPipedProbabilityList();
        }
    }
}
