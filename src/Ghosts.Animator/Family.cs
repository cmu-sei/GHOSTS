// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;

namespace Ghosts.Animator
{
    public static class Family
    {
        public static IEnumerable<FamilyProfile.Person> GetMembers()
        {
            var list = new List<FamilyProfile.Person>();
            for (var i = 0; i < AnimatorRandom.Rand.Next(0, 5); i++)
            {
                list.Add(GetMember());
            }

            return list;
        }

        public static FamilyProfile.Person GetMember()
        {
            if (Npc.NpcProfile != null)
            {
                var name = Name.GetName();
                if (Npc.NpcProfile.BiologicalSex == BiologicalSex.Female)
                    name.First = Name.GetFirstName(BiologicalSex.Male);
                if (Npc.NpcProfile.BiologicalSex == BiologicalSex.Male)
                    name.First = Name.GetFirstName(BiologicalSex.Female);
                return new FamilyProfile.Person { Name = name, Relationship = GetRelationship() };
            }
            return new FamilyProfile.Person { Name = Name.GetName(), Relationship = GetRelationship() };
        }

        public static string GetRelationship()
        {
            return RELATIONSHIPS.RandomElement();
        }

        private static readonly string[] RELATIONSHIPS = { "Spouse", "Parent", "Sibling", "Child" };
    }
}
