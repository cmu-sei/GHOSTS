using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator.Extensions;

namespace Ghosts.Animator.Services
{
    public static class AttributesService
    {
        public static Dictionary<string, string> GetAttributes()
        {
            var needs = new List<string>();

            if (PercentOfRandom.Does(98)) //x% of people need supplies
            {
                for (var i = 0; i < AnimatorRandom.Rand.Next(1, 4); i++)
                {
                    needs.Add(($"config/supplies.txt").GetRandomFromFile());
                }
            }

            var dict = new Dictionary<string, string>();
            dict.Add("Supply_Needs", string.Join(",", needs.Distinct()));
            return dict;
        }
    }
}
