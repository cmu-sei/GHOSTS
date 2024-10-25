using System;

namespace Ghosts.Animator.Models
{
    public class MotivationalProfile
    {
        public double Acceptance { get; set; }
        public double Beauty { get; set; }
        public double Curiosity { get; set; }
        public double Eating { get; set; }
        public double Family { get; set; }
        public double Honor { get; set; }
        public double Idealism { get; set; }
        public double Independence { get; set; }
        public double Order { get; set; }
        public double PhysicalActivity { get; set; }
        public double Power { get; set; }
        public double Saving { get; set; }
        public double SocialContact { get; set; }
        public double Status { get; set; }
        public double Tranquility { get; set; }
        public double Vengeance { get; set; }

        public static MotivationalProfile GetNew()
        {
            var m = new MotivationalProfile();
            var r = new Random();
            m.Acceptance = GetRandom(r);
            m.Beauty = GetRandom(r);
            m.Curiosity = GetRandom(r);
            m.Eating = GetRandom(r);
            m.Family = GetRandom(r);
            m.Honor = GetRandom(r);
            m.Idealism = GetRandom(r);
            m.Independence = GetRandom(r);
            m.Order = GetRandom(r);
            m.PhysicalActivity = GetRandom(r);
            m.Power = GetRandom(r);
            m.Saving = GetRandom(r);
            m.SocialContact = GetRandom(r);
            m.Status = GetRandom(r);
            m.Tranquility = GetRandom(r);
            m.Vengeance = GetRandom(r);

            return m;
        }

        private static double GetRandom(Random r)
        {
            var next = r.NextDouble();

            return (-2) + (next * (2 - (-2)));
        }
    }
}
