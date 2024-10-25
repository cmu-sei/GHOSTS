// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FileHelpers;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models.InsiderThreat;

namespace ghosts.api.Infrastructure.Models;

[DelimitedRecord(",")]
public class NPCToInsiderThreatCsv
{
    [FieldQuoted] public Guid Id { get; set; }
    [FieldQuoted] public string Hostname { get; set; }
    [FieldQuoted] public string DNS { get; set; }
    [FieldQuoted] public string OpenVPNUsername { get; set; }
    [FieldQuoted] public string OpenVPNPassword { get; set; }
    [FieldQuoted] public string GmailUsername { get; set; }
    [FieldQuoted] public string GmailPassword { get; set; }
    [FieldQuoted] public string NmailUser { get; set; }
    [FieldQuoted] public string NmailPassword { get; set; }
    [FieldQuoted] public string IPAddress { get; set; }
    [FieldQuoted] public string DomainUser { get; set; }
    [FieldQuoted] public string FirstName { get; set; }
    [FieldQuoted] public string LastName { get; set; }
    [FieldQuoted] public string Password { get; set; }
    [FieldQuoted] public string Company { get; set; }
    [FieldQuoted] public string StartDate { get; set; }
    [FieldQuoted] public string Department { get; set; }
    [FieldQuoted] public string Organization { get; set; }
    [FieldQuoted] public string JobTitle { get; set; }
    [FieldQuoted] public int JobLevel { get; set; }
    [FieldQuoted] public string Salary { get; set; }

    [FieldConverter(typeof(EmptyGuidConverter))]
    [FieldQuoted]
    public Guid Manager { get; set; }

    [FieldQuoted] public string EmailSuffix { get; set; }
    [FieldQuoted] public string Email { get; set; }
    [FieldQuoted] public string Type { get; set; }
    [FieldQuoted] public string Address { get; set; }
    [FieldQuoted] public string City { get; set; }
    [FieldQuoted] public string Phone { get; set; }
    [FieldQuoted] public string State { get; set; }
    [FieldQuoted] public string Zip { get; set; }
    [FieldQuoted] public string Country { get; set; }
    [FieldQuoted] public string EmploymentStatus { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Disgruntled { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Demoted { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool MissedRaises { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool TeamLayoffs { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool NotifiedOfTermination { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool AnnouncesTermination { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool AnnouncesResignation { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Threats { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool MissedPromotion { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Insubordination { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Absenteeism { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool HRComplaints { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ITPolicyViolations { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool IPPolicyViolations { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool DrugAlcoholAbuse { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool CoworkerConflict { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool EAPReferral { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool AccessRevoked { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool UnauthorizedCodingChange { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool UnauthorizedAccessChange { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FinancialProblems { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ArrestRecord { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool GamblingHistory { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ServiceAccountUse { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool RemoteAccess { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool BackdoorAccountUse { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool SolicitedByCompetitor { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool AfterHoursLogin { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool PrivilegeCreep { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FileExtensionModification { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FileHeaderModification { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FileContentModification { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FileTypeModification { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool SensitiveInformationCopied { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ScreenShots { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ZipFile { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Encryption { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool DocumentMarkingTampering { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Steganography { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool LogDeletion { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool ConcealmentInformation { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool SaleAttempt { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Scanner { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool CloudStorage { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool RemovableMedia { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool Print { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool NetworkShare { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool PersonalEmailAccount { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool EmailToConspirator { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool DNSExfiltrationTool { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool FileDeletion { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool RecentHRTicket { get; set; }

    [FieldConverter(typeof(TrueFalseToXConverter))]
    [FieldQuoted]
    public bool BackgroundCkResult { get; set; }

    [FieldQuoted] public int InterpersonalSkills { get; set; }
    [FieldQuoted] public int AdherenceToPolicy { get; set; }
    [FieldQuoted] public int EnthusiasmAndAttitude { get; set; }
    [FieldQuoted] public int OpenToFeedback { get; set; }
    [FieldQuoted] public int GeneralPerformance { get; set; }
    [FieldQuoted] public int OverallPerformance { get; set; }

    public static IEnumerable<NPCToInsiderThreatCsv> ConvertToCsv(IEnumerable<NpcRecord> npcs)
    {
        var finalList = new List<NPCToInsiderThreatCsv>();
        foreach (var n in npcs)
        {
            if (n.NpcProfile == null || n.NpcProfile.Accounts == null || n.NpcProfile.Address == null) continue;

            var events = n.NpcProfile.InsiderThreat.GetAllEvents();

            var i = AnimatorRandom.Rand.Next(0, 99);
            var o = new NPCToInsiderThreatCsv
            {
                Id = n.Id,
                Hostname = $"user{i}",
                DNS = $"FIN-USR-{i}"
            };

            foreach (var account in n.NpcProfile.Accounts.Where(x =>
                         x.Url != null && x.Url.ToLower().Contains("openvpn")))
            {
                o.OpenVPNUsername = account.Username;
                o.OpenVPNPassword = account.Password;
            }

            // o.HRMUserID
            // o.HRMManagerID
            // o.TestCase
            // o.ExampleCase

            foreach (var account in
                     n.NpcProfile.Accounts.Where(x => x.Url != null && x.Url.ToLower().Contains("gmail")))
            {
                o.GmailUsername = account.Username;
                o.GmailPassword = account.Password;
            }

            foreach (var account in
                     n.NpcProfile.Accounts.Where(x => x.Url != null && x.Url.ToLower().Contains("nmail")))
            {
                o.NmailUser = account.Username;
                o.NmailPassword = account.Password;
            }

            o.IPAddress = n.NpcProfile.Workstation.IPAddress;

            o.DomainUser = n.NpcProfile.Workstation.Username;
            o.FirstName = n.NpcProfile.Name.First;
            o.LastName = n.NpcProfile.Name.Last;
            o.Password = n.NpcProfile.Workstation.Password;
            o.Type = "User";
            if (n.NpcProfile.Employment.EmploymentRecords != null && n.NpcProfile.Employment.EmploymentRecords.Any())
            {
                var currentEmployer = n.NpcProfile.Employment.EmploymentRecords.LastOrDefault();
                o.EmploymentStatus = "Employed";
                o.Company = currentEmployer?.Company;
                o.StartDate = currentEmployer?.StartDate.ToString(CultureInfo.InvariantCulture);
                o.Department = currentEmployer?.Department;
                o.Organization = currentEmployer?.Organization;
                o.JobTitle = currentEmployer?.JobTitle;
                o.JobLevel = currentEmployer.Level;
                o.Salary = currentEmployer?.Salary.ToString(CultureInfo.InvariantCulture);
                o.Manager = currentEmployer.Manager;
                if (o.JobLevel > 2)
                {
                    if (AnimatorRandom.Rand.Next(0, 100) > 50)
                    {
                        o.Type = "Administrator";
                    }
                }
            }
            else
            {
                o.EmploymentStatus = "Unemployed";
            }

            o.EmailSuffix = n.NpcProfile.Email.After("@");
            o.Email = n.NpcProfile.Email;

            o.Address = n.NpcProfile.Address.FirstOrDefault()?.Address1;
            o.City = n.NpcProfile.Address.FirstOrDefault()?.City;
            o.Phone = n.NpcProfile.CellPhone;
            o.State = n.NpcProfile.Address.FirstOrDefault()?.State;
            o.Zip = n.NpcProfile.Address.FirstOrDefault()?.PostalCode;
            o.Country = "US";
            var relatedEvents = events as RelatedEvent[] ?? events.ToArray();
            if (!relatedEvents.Any()) continue;
            try
            {
                o.Demoted = relatedEvents.Any(x =>
                    x.Description.Contains("Demoted", StringComparison.CurrentCultureIgnoreCase));
                o.Disgruntled = relatedEvents.Length > 3;
                o.MissedRaises = relatedEvents.Any(x =>
                    x.Description.Contains("Missed raise", StringComparison.CurrentCultureIgnoreCase));
                o.TeamLayoffs = relatedEvents.Any(x =>
                    x.Description.Contains("Team layoffs", StringComparison.CurrentCultureIgnoreCase));
                o.NotifiedOfTermination = relatedEvents.Any(x =>
                    x.Description.Contains("Notified of termination", StringComparison.CurrentCultureIgnoreCase));
                o.AnnouncesTermination = relatedEvents.Any(x =>
                    x.Description.Contains("Announces Termination", StringComparison.CurrentCultureIgnoreCase));
                o.AnnouncesResignation = relatedEvents.Any(x =>
                    x.Description.Contains("Announces Resignation", StringComparison.CurrentCultureIgnoreCase));
                o.Threats = relatedEvents.Any(x =>
                                x.Description.Contains("Threatening", StringComparison.CurrentCultureIgnoreCase)) ||
                            relatedEvents.Any(x =>
                                x.Description.Contains("threatened", StringComparison.CurrentCultureIgnoreCase));
                o.MissedPromotion = relatedEvents.Any(x =>
                    x.Description.Contains("Missed Promotion", StringComparison.CurrentCultureIgnoreCase));
                o.Insubordination = relatedEvents.Any(x =>
                    x.Description.Contains("insubordinate", StringComparison.CurrentCultureIgnoreCase));
                o.Absenteeism = relatedEvents.Any(x =>
                    x.Description.Contains("missed work", StringComparison.CurrentCultureIgnoreCase));
                o.HRComplaints = relatedEvents.Any(x =>
                    x.Description.Contains("Human Resource Complaint", StringComparison.CurrentCultureIgnoreCase));
                o.ITPolicyViolations = relatedEvents.Any(x =>
                    x.Description.Contains("Employee violated company IT policy",
                        StringComparison.CurrentCultureIgnoreCase));
                o.IPPolicyViolations = relatedEvents.Any(x =>
                    x.Description.Contains("Compliance Violation", StringComparison.CurrentCultureIgnoreCase));
                o.DrugAlcoholAbuse = n.NpcProfile.InsiderThreat.SubstanceAbuseAndAddictiveBehaviors.RelatedEvents.Count != 0;
                o.CoworkerConflict = relatedEvents.Any(x =>
                    x.Description.Contains("coworker", StringComparison.CurrentCultureIgnoreCase));
                o.EAPReferral = relatedEvents.Any(x =>
                    x.Description.Contains("EAP Referral", StringComparison.CurrentCultureIgnoreCase));
                o.AccessRevoked = relatedEvents.Any(x =>
                    x.Description.Contains("Access Revoked", StringComparison.CurrentCultureIgnoreCase));
                o.UnauthorizedCodingChange = relatedEvents.Any(x =>
                    x.Description.Contains("unauthorized changes to a code base",
                        StringComparison.CurrentCultureIgnoreCase));
                o.UnauthorizedAccessChange = relatedEvents.Any(x =>
                    x.Description.Contains("unauthorized changes to access",
                        StringComparison.CurrentCultureIgnoreCase));
                o.FinancialProblems = relatedEvents.Any(x =>
                                          x.Description.Contains("Financial Problems",
                                              StringComparison.CurrentCultureIgnoreCase)) ||
                                      n.NpcProfile.InsiderThreat.FinancialConsiderations.RelatedEvents.Count != 0;
                o.ArrestRecord = relatedEvents.Any(x =>
                    x.Description.Contains("arrest", StringComparison.CurrentCultureIgnoreCase));
                o.GamblingHistory = relatedEvents.Any(x =>
                    x.Description.Contains("gambling", StringComparison.CurrentCultureIgnoreCase));
                o.ServiceAccountUse = relatedEvents.Any(x =>
                    x.Description.Contains("service account", StringComparison.CurrentCultureIgnoreCase));
                o.RemoteAccess = relatedEvents.Any(x =>
                    x.Description.Contains("Virtual Access Anomaly", StringComparison.CurrentCultureIgnoreCase));
                o.BackdoorAccountUse = relatedEvents.Any(x =>
                    x.Description.Contains("backdoor account", StringComparison.CurrentCultureIgnoreCase));
                o.SolicitedByCompetitor = relatedEvents.Any(x =>
                    x.Description.Contains("Solicited by Competitor", StringComparison.CurrentCultureIgnoreCase));
                o.AfterHoursLogin = relatedEvents.Any(x =>
                    x.Description.Contains("After Hours Login", StringComparison.CurrentCultureIgnoreCase));
                o.PrivilegeCreep = relatedEvents.Any(x =>
                    x.Description.Contains("Misusing Privileged Function", StringComparison.CurrentCultureIgnoreCase));
                o.FileExtensionModification = relatedEvents.Any(x =>
                    x.Description.Contains("modified file extension", StringComparison.CurrentCultureIgnoreCase));
                o.FileHeaderModification = relatedEvents.Any(x =>
                    x.Description.Contains("modified file header", StringComparison.CurrentCultureIgnoreCase));
                o.FileContentModification = relatedEvents.Any(x =>
                    x.Description.Contains("altered document", StringComparison.CurrentCultureIgnoreCase));
                o.FileTypeModification = relatedEvents.Any(x =>
                    x.Description.Contains("modified file extension", StringComparison.CurrentCultureIgnoreCase));
                o.SensitiveInformationCopied = relatedEvents.Any(x =>
                    x.Description.Contains("copied sensitive information", StringComparison.CurrentCultureIgnoreCase));
                o.ScreenShots = relatedEvents.Any(x =>
                    x.Description.Contains("took screenshots", StringComparison.CurrentCultureIgnoreCase));
                o.ZipFile = relatedEvents.Any(x =>
                    x.Description.Contains("compressed files", StringComparison.CurrentCultureIgnoreCase));
                o.Encryption = relatedEvents.Any(x =>
                    x.Description.Contains("encrypted files", StringComparison.CurrentCultureIgnoreCase));
                o.DocumentMarkingTampering = relatedEvents.Any(x =>
                    x.Description.Contains("altered document markings", StringComparison.CurrentCultureIgnoreCase));
                o.Steganography = relatedEvents.Any(x =>
                    x.Description.Contains("used steganography", StringComparison.CurrentCultureIgnoreCase));
                o.LogDeletion = relatedEvents.Any(x =>
                    x.Description.Contains("deleted logs", StringComparison.CurrentCultureIgnoreCase));
                o.ConcealmentInformation = relatedEvents.Any(x =>
                    x.Description.Contains("concealed actions", StringComparison.CurrentCultureIgnoreCase));
                o.SaleAttempt = relatedEvents.Any(x =>
                    x.Description.Contains("Information Sale Attempt", StringComparison.CurrentCultureIgnoreCase));
                o.Scanner = relatedEvents.Any(x =>
                    x.Description.Contains("scanned files", StringComparison.CurrentCultureIgnoreCase));
                o.CloudStorage = relatedEvents.Any(x =>
                    x.Description.Contains("cloud storage", StringComparison.CurrentCultureIgnoreCase));
                o.RemovableMedia = relatedEvents.Any(x =>
                    x.Description.Contains("removable media device", StringComparison.CurrentCultureIgnoreCase));
                o.Print = relatedEvents.Any(x =>
                    x.Description.Contains("printed sensitive files", StringComparison.CurrentCultureIgnoreCase));
                o.NetworkShare = relatedEvents.Any(x =>
                    x.Description.Contains("unauthorized changes", StringComparison.CurrentCultureIgnoreCase));
                o.PersonalEmailAccount = relatedEvents.Any(x =>
                    x.Description.Contains("personal email account", StringComparison.CurrentCultureIgnoreCase));
                o.EmailToConspirator = relatedEvents.Any(x =>
                    x.Description.Contains("Email to Conspirator", StringComparison.CurrentCultureIgnoreCase));
                o.DNSExfiltrationTool = relatedEvents.Any(x =>
                    x.Description.Contains("dns exfiltration", StringComparison.CurrentCultureIgnoreCase));
                o.FileDeletion = relatedEvents.Any(x =>
                    x.Description.Contains("deleted files", StringComparison.CurrentCultureIgnoreCase));
                o.RecentHRTicket =
                    relatedEvents.Any(x => x.Reported > DateTime.Now.AddYears(-1)); //reported in the last year
                o.BackgroundCkResult = n.NpcProfile.InsiderThreat.IsBackgroundCheckStatusClear;
                o.InterpersonalSkills = n.NpcProfile.MentalHealth.InterpersonalSkills;
                o.AdherenceToPolicy = n.NpcProfile.MentalHealth.AdherenceToPolicy;
                o.EnthusiasmAndAttitude = n.NpcProfile.MentalHealth.EnthusiasmAndAttitude;
                o.OpenToFeedback = n.NpcProfile.MentalHealth.OpenToFeedback;
                o.GeneralPerformance = n.NpcProfile.MentalHealth.GeneralPerformance;
                o.OverallPerformance = n.NpcProfile.MentalHealth.OverallPerformance;
            }
            catch (Exception e)
            {
                //todo log this
                Console.WriteLine(e);
            }
            finalList.Add(o);
        }

        foreach (var npc in finalList.Where(x => x.Manager != Guid.Empty))
        {
            var manager = finalList.FirstOrDefault(x => x.Id == npc.Manager);
            if (manager == null) continue;
            if (npc.Company != manager.Company || npc.Department != manager.Department)
            {
                npc.Manager = Guid.Empty;
            }
        }

        return finalList;
    }
}

public class TrueFalseToXConverter : ConverterBase
{
    public override object StringToField(string o)
    {
        return o.Equals("X", StringComparison.InvariantCultureIgnoreCase);
    }

    public override string FieldToString(object o)
    {
        return Convert.ToBoolean(o) ? "X" : string.Empty;
    }
}

public class EmptyGuidConverter : ConverterBase
{
    public override object StringToField(string o)
    {
        return o.Equals(Guid.Empty.ToString(), StringComparison.InvariantCultureIgnoreCase);
    }

    public override string FieldToString(object o)
    {
        var x = Guid.Parse(o.ToString() ?? "");
        return x == Guid.Empty ? "" : x.ToString();
    }
}
