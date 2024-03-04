// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Models.InsiderThreat;

namespace Ghosts.Animator.Models
{
    public class NpcProfileSummary
    {
        public Guid Id { get; set; }
        public NameProfile Name { get; set; } = new();
        public IList<AddressProfiles.AddressProfile> Address { get; set; } = new List<AddressProfiles.AddressProfile>();
        public string Email { get; set; }
        public string Password { get; set; }
        public string HomePhone { get; set; }
        public string CellPhone { get; set; }
        public MilitaryUnit Unit { get; set; } = new();
        public MilitaryRank.Branch.Rank Rank { get; set; } = new();
        public EducationProfile Education { get; set; } = new();
        public EmploymentProfile Employment { get; set; } = new();
        public BiologicalSex BiologicalSex { get; set; }
        public DateTime Birthdate { get; set; }
        public HealthProfile Health { get; set; } = new();
        public Dictionary<string, string> Attributes { get; set; } = new();
        public IEnumerable<RelationshipProfile> Relationships { get; set; } = new List<RelationshipProfile>();
        public FamilyProfile Family { get; set; } = new();
        public FinancialProfile Finances { get; set; } = new();
        public MentalHealthProfile MentalHealth { get; set; } = new();
        public ForeignTravelProfile ForeignTravel { get; set; } = new();
        public CareerProfile Career { get; set; } = new();
        public InsiderThreatProfile InsiderThreat { get; set; } = new();
        public IEnumerable<AccountsProfile.Account> Accounts { get; set; }
        public MotivationalProfile MotivationalProfile { get; set; } = new();
        public DateTime Created { get; set; } = DateTime.UtcNow;
    }
}