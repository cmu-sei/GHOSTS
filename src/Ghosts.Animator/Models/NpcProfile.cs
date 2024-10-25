// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Models.InsiderThreat;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Models
{
    public class NpcProfile
    {
        public Guid Id { get; set; }

        public NameProfile Name { get; set; }
        public IList<AddressProfiles.AddressProfile> Address { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string HomePhone { get; set; }
        public string CellPhone { get; set; }

        public IEnumerable<Preference> Preferences { get; set; }
        public MilitaryUnit Unit { get; set; }
        public MilitaryRank.Branch.Rank Rank { get; set; }

        public EducationProfile Education { get; set; }
        public EmploymentProfile Employment { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public BiologicalSex BiologicalSex { get; set; }
        public DateTime Birthdate { get; set; }

        public HealthProfile Health { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public IEnumerable<RelationshipProfile> Relationships { get; set; }

        public FamilyProfile Family { get; set; }
        public FinancialProfile Finances { get; set; }
        public MentalHealthProfile MentalHealth { get; set; }
        public ForeignTravelProfile ForeignTravel { get; set; }
        public CareerProfile Career { get; set; }

        public MachineProfile Workstation { get; set; }

        public InsiderThreatProfile InsiderThreat { get; set; }

        public IEnumerable<AccountsProfile.Account> Accounts { get; set; }

        public MotivationalProfile MotivationalProfile { get; set; }
        public string CAC { get; set; }

        public string PhotoLink { get; set; }
        public DateTime Created { get; set; }

        public NpcProfile()
        {
            Created = DateTime.UtcNow;
            Address = new List<AddressProfiles.AddressProfile>();
            Career = new CareerProfile();
            Family = new FamilyProfile();
            Finances = new FinancialProfile();
            ForeignTravel = new ForeignTravelProfile();
            InsiderThreat = new InsiderThreatProfile();
            MentalHealth = new MentalHealthProfile();
            Name = new NameProfile();
            Rank = new MilitaryRank.Branch.Rank();
            Unit = new MilitaryUnit();
            Workstation = new MachineProfile();
            Education = new EducationProfile();
            Employment = new EmploymentProfile();
            Relationships = new List<RelationshipProfile>();
            Health = new HealthProfile();
            Attributes = new Dictionary<string, string>();
            MotivationalProfile = new MotivationalProfile();
        }

        public void SetName(string o)
        {
            if (!o.Contains("."))
            {
                Name.First = o;
            }
            else
            {
                var a = o.Split('.');
                Name.First = a[0];
                Name.Last = a[a.GetUpperBound(0)];
            }
        }
    }

    public class MachineProfile
    {
        public string Name { get; set; }
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string IPAddress { get; set; }
    }

    public class NameProfile
    {
        public string Prefix { get; set; }
        public string First { get; set; }
        public string Middle { get; set; }
        public string Last { get; set; }

        public string Suffix { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(First))
            {
                return $"{Last}";
            }

            if (string.IsNullOrEmpty(Last))
            {
                return string.IsNullOrEmpty(Middle) ? $"{First}" : $"{First} {Middle}";
            }

            return string.IsNullOrEmpty(Middle) ? $"{First} {Last}" : $"{First} {Middle} {Last}";
        }
    }

    public class FamilyProfile
    {
        public IEnumerable<Person> Members { get; set; }

        public FamilyProfile()
        {
            Members = new List<Person>();
        }

        public class Person
        {
            public NameProfile Name { get; set; }
            public string Relationship { get; set; }
        }
    }

    public class FinancialProfile
    {
        public double NetWorth { get; set; }
        public double TotalDebt { get; set; }
        public IEnumerable<CreditCard> CreditCards { get; set; }

        public FinancialProfile()
        {
            CreditCards = new List<CreditCard>();
        }

        public class CreditCard
        {
            public string Number { get; set; }
            public string Type { get; set; }
        }
    }

    public class MentalHealthProfile
    {
        /// <summary>
        /// EmotionalIntelligence
        /// </summary>
        public int InterpersonalSkills { get; set; }
        public int AdherenceToPolicy { get; set; }
        public int EnthusiasmAndAttitude { get; set; }
        public int OpenToFeedback { get; set; }
        public int GeneralPerformance { get; set; }
        public int OverallPerformance { get; set; }

        public int IQ { get; set; }
        public int SpideySense { get; set; }
        public int SenseSomethingIsWrongQuotient { get; set; }
        public int HappyQuotient { get; set; }
        public int MelancholyQuotient { get; set; }
    }
}
