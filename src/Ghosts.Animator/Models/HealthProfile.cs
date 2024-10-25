using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class HealthProfile
    {
        public int Height { get; set; } //inches
        public int Weight { get; set; } //lbs
        public string BloodType { get; set; }

        public string PreferredMeal { get; set; }
        public List<MedicalCondition> MedicalConditions { get; set; }

        public HealthProfile()
        {
            MedicalConditions = new List<MedicalCondition>();
        }
    }

    public class MedicalCondition
    {
        public string Name { get; set; }

        public List<Prescription> Prescriptions { get; set; }

        public MedicalCondition()
        {
            Prescriptions = new List<Prescription>();
        }
    }

    public class Prescription
    {
        public string Name { get; set; }
    }
}
