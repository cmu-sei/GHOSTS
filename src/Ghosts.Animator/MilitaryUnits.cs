// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Ghosts.Animator.Services;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class MilitaryUnits
    {
        public static MilitaryBranch GetServiceBranch()
        {
            var o = Enum.GetValues(typeof(MilitaryBranch)).Cast<MilitaryBranch>().ToList();
            return o.RandomElement();
        }

        public static MilitaryUnit GetAll()
        {
            var o = GetAllEx();
            return o;
        }

        public static IEnumerable<MilitaryUnit.Unit> GetAllByServiceBranch(MilitaryBranch branch)
        {
            var o = GetAllEx();
            return o.Sub.Where(x => x.Nick == branch.ToString());
        }

        public static MilitaryUnit GetOneByServiceBranch(MilitaryBranch branch)
        {
            var choice = new MilitaryUnitService(branch, GetAllEx());

            var hq = new MilitaryUnitAddressService(choice.Unit);
            if (!string.IsNullOrEmpty(hq?.MilUnit?.Address?.Name))
            {
                choice.Unit.Address = GetBaseAddress(branch, hq.MilUnit.Address.Name);
            }
            else
            {
                choice.Unit.Address = new AddressProfiles.AddressProfile();
            }

            return choice.Unit;
        }

        public static AddressProfiles.AddressProfile GetBaseAddress(MilitaryBranch branch, string hq)
        {
            var a = new AddressProfiles.AddressProfile();

            var raw = File.ReadAllText("config/military_bases.json");
            var o = JsonConvert.DeserializeObject<MilitaryBases.BaseManager>(raw);

            var b = o.Branches.FirstOrDefault(x => x.Name == branch.ToString());
            var myBase = b.Bases.FirstOrDefault(x => x.Name.Equals(hq, StringComparison.InvariantCultureIgnoreCase)) ?? (o.Branches.FirstOrDefault(x => x.Name == branch.ToString())?.Bases.RandomElement());
            if (myBase == null)
                return null;

            a.AddressType = "Base";
            a.Name = myBase.Name;
            if (myBase.Streets.Any())
            {
                a.Address1 = myBase.Streets.RandomElement();
            }
            if (string.IsNullOrEmpty(a.Address1))
                a.Address1 = Address.GetStreetAddress();
            a.City = myBase.City;
            a.State = myBase.State;
            a.PostalCode = myBase.PostalCode;
            if (string.IsNullOrEmpty(a.State))
            {
                a.State = Address.GetUSStateAbbreviation();
            }
            if (string.IsNullOrEmpty(a.City))
            {
                var cityAndZip = Address.GetCityAndZipFromStateAbbreviation(a.State);
                a.City = cityAndZip["City"];
                a.PostalCode = cityAndZip["ZipCode"];
            }
            return a;
        }

        private static MilitaryUnit GetAllEx()
        {
            var raw = File.ReadAllText("config/military_unit.json");
            var o = JsonConvert.DeserializeObject<MilitaryUnit>(raw);
            return o;
        }
    }
}
