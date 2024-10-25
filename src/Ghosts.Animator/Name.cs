// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;

namespace Ghosts.Animator
{
    public static class Name
    {
        public static Models.NameProfile GetName()
        {
            switch (AnimatorRandom.Rand.Next(4))
            {
                case 0: return new Models.NameProfile { Prefix = GetPrefix(), First = GetFirstName(), Last = GetLastName() };
                case 1: return new Models.NameProfile { First = GetFirstName(), Last = GetLastName(), Suffix = GetSuffix() };
                case 2: return new Models.NameProfile { First = GetFirstName(), Last = GetLastName(), Middle = GetMiddleName() };
                default: return new Models.NameProfile { First = GetFirstName(), Last = GetLastName() };
            }
        }

        public static string GetFirstName()
        {
            var file = $"config/names_{PhysicalCharacteristics.GetBiologicalSex().ToString().ToLower()}.txt";
            if (Npc.NpcProfile != null)
            {
                file = $"config/names_{Npc.NpcProfile.BiologicalSex.ToString().ToLower()}.txt";
            }

            return file.GetRandomFromFile();
        }

        public static string GetFirstName(BiologicalSex sex)
        {
            var file = $"config/names_{sex.ToString().ToLower()}.txt";
            return file.GetRandomFromFile();
        }

        public static string GetMiddleName()
        {
            return GetFirstName();
        }

        public static string GetLastName()
        {
            return "config/names_last.txt".GetRandomFromFile();
        }

        public static string GetPrefix()
        {
            return PREFIXES.RandomElement();
        }

        public static string GetSuffix()
        {
            return SUFFIXES.RandomElement();
        }

        static readonly string[] PREFIXES = { "Mr.", "Mrs.", "Ms.", "Miss", "Dr." };

        static readonly string[] SUFFIXES = { "Jr.", "Sr.", "I", "II", "III", "IV", "V", "MD", "DDS", "PhD", "DVM" };
    }
}
