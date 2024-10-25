using System.Collections.Generic;
using System.IO;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator.Services
{
    public static class HealthService
    {
        public static HealthProfile GetHealthProfile()
        {
            var o = new HealthProfile();

            if (Npc.NpcProfile.Rank == null)
            {
                Npc.NpcProfile.Rank = new MilitaryRank.Branch.Rank
                {
                    Branch = MilitaryBranch.USARMY,
                    Pay = "0"
                };
            }

            o.Height = PhysicalCharacteristics.GetMilHeight(Npc.NpcProfile.BiologicalSex, Npc.NpcProfile.Rank.Branch);
            o.Weight = PhysicalCharacteristics.GetMilWeight(o.Height, Npc.NpcProfile.Birthdate, Npc.NpcProfile.BiologicalSex, Npc.NpcProfile.Rank.Branch);
            o.BloodType = PhysicalCharacteristics.GetBloodType();

            var mealPreference = string.Empty;

            if (PercentOfRandom.Does(95)) //x% have a meal preference
            {
                mealPreference = ($"config/meal_preferences.txt").GetRandomFromFile();
            }
            o.PreferredMeal = mealPreference;

            if (PercentOfRandom.Does(98)) //x% have a medical condition
            {
                var raw = File.ReadAllText("config/medical_conditions_and_medications.json");
                var r = JsonConvert.DeserializeObject<IEnumerable<HealthProfileRecord>>(raw).RandomElement();

                var c = new MedicalCondition { Name = r.Condition };
                foreach (var med in r.Medications)
                    c.Prescriptions.Add(new Prescription { Name = med });
                o.MedicalConditions.Add(c);
            }

            return o;
        }

        public class HealthProfileRecord
        {
            public string Condition { get; set; }
            public IList<string> Medications { get; set; }
        }
    }
}
