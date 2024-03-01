// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Animator.Models;
using Ghosts.Animator.Services;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class Npc
    {
        public static NpcProfile NpcProfile { get; set; }

        public static NpcProfile Generate()
        {
            return Generate(new NpcGenerationConfiguration { Branch = MilitaryUnits.GetServiceBranch()});
        }
        
        public static NpcProfile Generate(Enums.MilitaryBranch branch)
        {
            return Generate(new NpcGenerationConfiguration { Branch = branch});
        }
        
        public static NpcProfile Generate(Enums.MilitaryBranch branch, string username)
        {
            return Generate(new NpcGenerationConfiguration { Branch = branch}, username);
        }
        
        public static NpcProfile Generate(NpcGenerationConfiguration config, string username = null)
        {
            if(!config.Branch.HasValue)
                config.Branch = MilitaryUnits.GetServiceBranch();
        
            NpcProfile = new NpcProfile
            {
                Id = Guid.NewGuid()
            };

            NpcProfile.Unit = MilitaryUnits.GetOneByServiceBranch(config.Branch.Value);
            NpcProfile.Rank = MilitaryRanks.GetRankByBranch(config.Branch.Value);
            NpcProfile.BiologicalSex = PhysicalCharacteristics.GetBiologicalSex();

            if(NpcProfile.Rank?.Pay != null)
                NpcProfile.Birthdate = PhysicalCharacteristics.GetBirthdate(NpcProfile.Rank.Pay);
            NpcProfile.Health = HealthService.GetHealthProfile();
            
            NpcProfile.Address.Add(Address.GetHomeAddress());

            if(string.IsNullOrEmpty(username))
                NpcProfile.Name = Name.GetName();
            else
                NpcProfile.SetName(username);

            NpcProfile.Email = Internet.GetMilEmail(NpcProfile.Name.ToString());
            NpcProfile.Password = Internet.GetPassword();
            NpcProfile.CellPhone = PhoneNumber.GetPhoneNumber();
            NpcProfile.HomePhone = PhoneNumber.GetPhoneNumber();

            NpcProfile.Workstation.Domain = Internet.GetMilDomainName();
            NpcProfile.Workstation.Name = Internet.GetComputerName();
            NpcProfile.Workstation.Password = Internet.GetPassword();
            NpcProfile.Workstation.Username = Internet.GetMilUserName(NpcProfile.Name.ToString());
            NpcProfile.Workstation.IPAddress = $"192.168.{AnimatorRandom.Rand.Next(2, 254)}.{AnimatorRandom.Rand.Next(2, 254)}";

            NpcProfile.Career.Strengths = Career.GetStrengths();
            NpcProfile.Career.Weaknesses = Career.GetWeaknesses();
            NpcProfile.Career.WorkEthic = Career.GetWorkEthic();
            NpcProfile.Career.TeamValue = Career.GetTeamValue();
            NpcProfile.Employment = EmploymentHistory.GetEmployment();

            NpcProfile.Family.Members = Family.GetMembers();

            NpcProfile.Finances.CreditCards = CreditCard.GetCreditCards();
            NpcProfile.Finances.NetWorth = CreditCard.GetNetWorth();
            NpcProfile.Finances.TotalDebt = CreditCard.GetTotalDebt();

            NpcProfile.ForeignTravel.Trips = Travel.GetTrips();

            NpcProfile.MentalHealth = MentalHealth.GetMentalHealth();
            NpcProfile.Accounts = Internet.GetAccounts(NpcProfile.Name.ToString());
            NpcProfile.Education = Education.GetMilEducationProfile(NpcProfile.Rank);

            NpcProfile.InsiderThreat = InsiderThreat.GetInsiderThreatProfile();

            NpcProfile.PhotoLink = PhysicalCharacteristics.GetPhotoUrl();

            NpcProfile.Attributes = AttributesService.GetAttributes();

            NpcProfile.MotivationalProfile = MotivationalProfile.GetNew();
                
            return NpcProfile;
        }

        public static NpcProfile LoadFromFile(string filePath)
        {
            try
            {
                return JsonConvert.DeserializeObject<NpcProfile>(filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading file: {e}");
                return null;
            }
        }
    }
}