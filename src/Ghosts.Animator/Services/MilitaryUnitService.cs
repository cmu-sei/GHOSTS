// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;

namespace Ghosts.Animator.Services
{
    public class MilitaryUnitService
    {
        public MilitaryUnit Unit { get; set; }

        public MilitaryUnitService(MilitaryBranch branch, MilitaryUnit units)
        {
            var unit = units.Sub.ToList().FirstOrDefault(x => x.Nick == branch.ToString());

            Unit = units.Clone();
            Unit.Sub = null;

            if (unit != null)
            {
                var currentUnit = unit.Clone();
                var list = new List<MilitaryUnit.Unit>();

                while (currentUnit != null)
                {
                    if (currentUnit.Sub == null)
                    {
                        break;
                    }

                    currentUnit = GetUnit(currentUnit);
                    list.Add(currentUnit);
                }

                list.Reverse();
                List<MilitaryUnit.Unit> previous = null;
                foreach (var item in list)
                {
                    if (previous == null)
                    {
                        previous = new List<MilitaryUnit.Unit>();
                    }

                    previous = SetUnit(previous, new List<MilitaryUnit.Unit> { item });
                }

                Unit.Sub = previous;
            }

            Unit.Country = units.Country;
        }

        private static List<MilitaryUnit.Unit> SetUnit(List<MilitaryUnit.Unit> previous, List<MilitaryUnit.Unit> current)
        {
            foreach (var item in current)
            {
                item.Sub = previous;
            }

            return new List<MilitaryUnit.Unit> { current[0] };
        }

        private static MilitaryUnit.Unit GetUnit(MilitaryUnit.Unit unit)
        {
            return unit.Sub == null ? null : !unit.Sub.Any() ? null : unit.Sub.ToList().RandomElement().Clone();
        }
    }

    public class MilitaryUnitAddressService
    {
        public MilitaryUnit MilUnit { get; private set; }

        public MilitaryUnitAddressService(MilitaryUnit militaryUnit)
        {
            MilUnit = militaryUnit;

            var currentUnit = militaryUnit.Sub.First();

            while (currentUnit != null)
            {
                if (currentUnit.Sub == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(currentUnit.HQ))
                {
                    MilUnit.Address = new AddressProfiles.AddressProfile
                    {
                        Name = currentUnit.HQ
                    };
                }

                currentUnit = GetUnit(currentUnit);
            }
        }

        private static MilitaryUnit.Unit GetUnit(MilitaryUnit.Unit unit)
        {
            return unit.Sub == null ? null : !unit.Sub.Any() ? null : unit.Sub.First();
        }
    }
}
