// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator.Extensions;

namespace Ghosts.Animator.Services
{
    public static class PreferenceService
    {
        /// <summary>
        /// The interests an npc leans toward, scored 1-100. Chat personas read the highest scoring
        /// of these, so an npc generated without any has nothing to talk about.
        /// </summary>
        public static IEnumerable<Preference> GetPreferences()
        {
            var topics = new List<string>();
            for (var i = 0; i < AnimatorRandom.Rand.Next(3, 9); i++)
            {
                topics.Add(("config/knowledge_topics.txt").GetRandomFromFile());
            }

            return topics.Distinct().Select((topic, i) => new Preference
            {
                Id = i,
                Name = topic,
                Score = AnimatorRandom.Rand.Next(1, 100)
            }).ToList();
        }
    }
}
