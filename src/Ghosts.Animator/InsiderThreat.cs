// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models.InsiderThreat;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class InsiderThreat
    {
        public static InsiderThreatProfile GetInsiderThreatProfile()
        {
            var raw = File.ReadAllText("config/insider_threat.json");
            var o = JsonConvert.DeserializeObject<InsiderThreatManager>(raw);

            var insiderThreatProfile = new InsiderThreatProfile();

            foreach (var profile in o.Profiles)
            {
                if (profile == null || !profile.Items.Any()) continue;

                // some random % get a violation get violation from o
                if (AnimatorRandom.Rand.Next(0, 100) > 72)
                {
                    var selectedEvent = profile.Items.RandomElement();

                    var newEvent = new RelatedEvent
                    {
                        Reported = AnimatorRandom.Date(),
                        Description = selectedEvent.Name,
                        ReportedBy = Name.GetName().ToString()
                    };
                    if (selectedEvent.Violation)
                    {
                        var c = o.CorrectiveActions.RandomElement();
                        newEvent.CorrectiveAction = c.Name;
                    }

                    switch (profile.Name)
                    {
                        case "AccessProfile":
                            insiderThreatProfile.Access.RelatedEvents.Add(newEvent);
                            break;
                        case "CriminalViolentOrAbusiveConductProfile":
                            insiderThreatProfile.CriminalViolentOrAbusiveConduct.RelatedEvents.Add(newEvent);
                            break;
                        case "FinancialConsiderationsProfile":
                            insiderThreatProfile.FinancialConsiderations.RelatedEvents.Add(newEvent);
                            break;
                        case "ForeignConsiderationsProfile":
                            if (Npc.NpcProfile.ForeignTravel.Trips.Any()) //probably need a trip in order to have event
                                insiderThreatProfile.ForeignConsiderations.RelatedEvents.Add(newEvent);
                            break;
                        case "JudgementCharacterAndPsychologicalConditionsProfile":
                            insiderThreatProfile.JudgementCharacterAndPsychologicalConditions.RelatedEvents.Add(newEvent);
                            break;
                        case "ProfessionalLifecycleAndPerformanceProfile":
                            insiderThreatProfile.ProfessionalLifecycleAndPerformance.RelatedEvents.Add(newEvent);
                            break;
                        case "SecurityAndComplianceIncidentsProfile":
                            insiderThreatProfile.SecurityAndComplianceIncidents.RelatedEvents.Add(newEvent);
                            break;
                        case "SubstanceAbuseAndAddictiveBehaviorsProfile":
                            insiderThreatProfile.SubstanceAbuseAndAddictiveBehaviors.RelatedEvents.Add(newEvent);
                            break;
                        case "TechnicalActivityProfile":
                            insiderThreatProfile.TechnicalActivity.RelatedEvents.Add(newEvent);
                            break;
                    }
                }
            }

            PopulateAccess(insiderThreatProfile);
            PopulateHrTickets(insiderThreatProfile);

            return insiderThreatProfile;
        }

        private static void PopulateAccess(InsiderThreatProfile insiderThreatProfile)
        {
            if (AnimatorRandom.Rand.Next(0, 100) > 85)
            {
                insiderThreatProfile.IsBackgroundCheckStatusClear = true;
                insiderThreatProfile.Access.SecurityClearance = new[] { "C", "S", "TS", "TS SCI" }.RandomElement();
                insiderThreatProfile.Access.SystemsAccess = AnimatorRandom.Rand.Next(0, 100) > 15 ? new[] { "SIPR", "MIPR", "JWICS" }.RandomElement() : "N/A";
                insiderThreatProfile.Access.CBRNAccess = AnimatorRandom.Rand.Next(0, 100) > 5 ? "Yes" : "N/A";
                insiderThreatProfile.Access.IsDoDSystemsPrivilegedUser = AnimatorRandom.Rand.Next(0, 100) > 5;
                insiderThreatProfile.Access.ExplosivesAccess = AnimatorRandom.Rand.Next(0, 100) > 95 ? $"ATF Form {AnimatorRandom.Rand.Next(0, 6)}" : "N/A";
                insiderThreatProfile.Access.PhysicalAccess = AnimatorRandom.Rand.Next(0, 100) > 15 ? new[] { "Secret-cleared spaces", "SCIF", "SAP" }.RandomElement() : "N/A";
            }
            else
            {
                insiderThreatProfile.IsBackgroundCheckStatusClear = false;
                insiderThreatProfile.Access.SecurityClearance = "N/A";
                insiderThreatProfile.Access.SystemsAccess = "N/A";
                insiderThreatProfile.Access.CBRNAccess = "N/A";
                insiderThreatProfile.Access.IsDoDSystemsPrivilegedUser = false;
                insiderThreatProfile.Access.ExplosivesAccess = "N/A";
                insiderThreatProfile.Access.PhysicalAccess = "N/A";
            }
        }

        private static void PopulateHrTickets(InsiderThreatProfile insiderThreatProfile)
        {
            var selectedTicket = new[] { "Policy Violation", "Disruptive Behavior", "Financial Hardship", "Job Performance Problem" }.RandomElement();
            var newEvent = new RelatedEvent
            {
                Reported = AnimatorRandom.Date(),
                Description = selectedTicket
            };
            insiderThreatProfile.TechnicalActivity.RelatedEvents.Add(newEvent);
        }

        public class CorrectiveAction
        {
            public string Name { get; set; }
            public double Probability { get; set; }
        }

        public class Item
        {
            public string Name { get; set; }
            public bool Violation { get; set; }
        }

        public class Profile
        {
            public string Name { get; set; }
            public IList<Item> Items { get; set; }
        }

        public class InsiderThreatManager
        {
            public IList<CorrectiveAction> CorrectiveActions { get; set; }
            public IList<Profile> Profiles { get; set; }
        }
    }
}
