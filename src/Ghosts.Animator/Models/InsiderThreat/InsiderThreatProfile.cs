// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models.InsiderThreat
{
    public class InsiderThreatProfile
    {
        public AccessProfile Access { get; set; }
        public CriminalViolentOrAbusiveConductProfile CriminalViolentOrAbusiveConduct { get; set; }
        public FinancialConsiderationsProfile FinancialConsiderations { get; set; }
        public ForeignConsiderationsProfile ForeignConsiderations { get; set; }
        public JudgementCharacterAndPsychologicalConditionsProfile JudgementCharacterAndPsychologicalConditions { get; set; }
        public ProfessionalLifecycleAndPerformanceProfile ProfessionalLifecycleAndPerformance { get; set; }
        public SecurityAndComplianceIncidentsProfile SecurityAndComplianceIncidents { get; set; }
        public SubstanceAbuseAndAddictiveBehaviorsProfile SubstanceAbuseAndAddictiveBehaviors { get; set; }
        public TechnicalActivityProfile TechnicalActivity { get; set; }
        public bool IsBackgroundCheckStatusClear { get; set; }

        public InsiderThreatProfile()
        {
            this.Access = new AccessProfile();
            this.CriminalViolentOrAbusiveConduct = new CriminalViolentOrAbusiveConductProfile();
            this.FinancialConsiderations = new FinancialConsiderationsProfile();
            this.ForeignConsiderations = new ForeignConsiderationsProfile();
            this.JudgementCharacterAndPsychologicalConditions = new JudgementCharacterAndPsychologicalConditionsProfile();
            this.ProfessionalLifecycleAndPerformance = new ProfessionalLifecycleAndPerformanceProfile();
            this.SecurityAndComplianceIncidents = new SecurityAndComplianceIncidentsProfile();
            this.SubstanceAbuseAndAddictiveBehaviors = new SubstanceAbuseAndAddictiveBehaviorsProfile();
            this.TechnicalActivity = new TechnicalActivityProfile();
        }

        public IEnumerable<RelatedEvent> GetAllEvents()
        {
            var events = new List<RelatedEvent>();
            events.AddRange(this.Access.RelatedEvents);
            events.AddRange(this.FinancialConsiderations.RelatedEvents);
            events.AddRange(this.ForeignConsiderations.RelatedEvents);
            events.AddRange(this.TechnicalActivity.RelatedEvents);
            events.AddRange(this.ProfessionalLifecycleAndPerformance.RelatedEvents);
            events.AddRange(this.SecurityAndComplianceIncidents.RelatedEvents);
            events.AddRange(this.CriminalViolentOrAbusiveConduct.RelatedEvents);
            events.AddRange(this.JudgementCharacterAndPsychologicalConditions.RelatedEvents);
            events.AddRange(this.SubstanceAbuseAndAddictiveBehaviors.RelatedEvents);
            return events;
        }
    }

    public class ForeignConsiderationsProfile : InsiderThreatBaseProfile { }
    public class TechnicalActivityProfile : InsiderThreatBaseProfile { }
    public class ProfessionalLifecycleAndPerformanceProfile : InsiderThreatBaseProfile { }
    public class SecurityAndComplianceIncidentsProfile : InsiderThreatBaseProfile { }
    public class CriminalViolentOrAbusiveConductProfile : InsiderThreatBaseProfile { }
    public class JudgementCharacterAndPsychologicalConditionsProfile : InsiderThreatBaseProfile { }
    public class SubstanceAbuseAndAddictiveBehaviorsProfile : InsiderThreatBaseProfile { }
    public class FinancialConsiderationsProfile : InsiderThreatBaseProfile { }

    public class AccessProfile : InsiderThreatBaseProfile
    {
        public string SecurityClearance { get; set; }
        public string PhysicalAccess { get; set; }
        public string SystemsAccess { get; set; }
        public bool? IsDoDSystemsPrivilegedUser { get; set; }
        public string ExplosivesAccess { get; set; }
        public string CBRNAccess { get; set; }
    }
}