// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Models.InsiderThreat;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator.Models
{
    public class NpcProfileSummary
    {
        public Guid Id { get; set; }
        public NameProfile Name { get; set; } = new NameProfile();
        public IList<AddressProfiles.AddressProfile> Address { get; set; } = new List<AddressProfiles.AddressProfile>();
        public string Email { get; set; }
        public string Password { get; set; }
        public string HomePhone { get; set; }
        public string CellPhone { get; set; }
        public MilitaryUnit Unit { get; set; } = new MilitaryUnit();
        public MilitaryRank.Branch.Rank Rank { get; set; } = new MilitaryRank.Branch.Rank();
        public EducationProfile Education { get; set; } = new EducationProfile();
        public EmploymentProfile Employment { get; set; } = new EmploymentProfile();
        [JsonConverter(typeof(StringEnumConverter))]
        public BiologicalSex BiologicalSex { get; set; }
        public DateTime Birthdate { get; set; }
        public HealthProfile Health { get; set; } = new HealthProfile();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public IEnumerable<RelationshipProfile> Relationships { get; set; } = new List<RelationshipProfile>();
        public FamilyProfile Family { get; set; } = new FamilyProfile();
        public FinancialProfile Finances { get; set; } = new FinancialProfile();
        public MentalHealthProfile MentalHealth { get; set; } = new MentalHealthProfile();
        public ForeignTravelProfile ForeignTravel { get; set; } = new ForeignTravelProfile();
        public CareerProfile Career { get; set; } = new CareerProfile();
        public InsiderThreatProfile InsiderThreat { get; set; } = new InsiderThreatProfile();
        public IEnumerable<AccountsProfile.Account> Accounts { get; set; }
        public MotivationalProfile MotivationalProfile { get; set; } = new MotivationalProfile();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public IEnumerable<Preference> Preferences { get; set; } = new List<Preference>();
    }
}
