using System.Globalization;

namespace Ghosts.Pandora.Infrastructure.Content;

/// <summary>
/// Writes the document or table a <see cref="ContentRequest"/> asks for, about the <see cref="World"/>.
/// Everything is drawn from the request's seed, so the same path always gives the same content.
/// </summary>
public class DocumentComposer(World world)
{
    private const string LongDate = "MMMM d, yyyy";

    // Money and percentages read the same whatever culture the server runs in
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static string Money(decimal value) => value.ToString("C0", Us);

    private static string Percent(decimal value) => value.ToString("P1", Us);

    public DocumentSpec Compose(ContentRequest request)
    {
        var rng = request.CreateRandom();
        return request.Kind switch
        {
            DocumentKind.NetworkAssessment => NetworkAssessment(request, rng),
            DocumentKind.IncidentReport => IncidentReport(request, rng),
            DocumentKind.Budget => Budget(request, rng),
            DocumentKind.Personnel => Personnel(request, rng),
            DocumentKind.MeetingMinutes => MeetingMinutes(request, rng),
            DocumentKind.Policy => Policy(request, rng),
            DocumentKind.ProjectStatus => ProjectStatus(request, rng),
            _ => Memo(request, rng)
        };
    }

    /// <summary>The table behind the request, for spreadsheets, CSV and JSON.</summary>
    public DataTable ComposeTable(ContentRequest request)
    {
        var rng = request.CreateRandom();
        return request.Kind switch
        {
            DocumentKind.NetworkAssessment => AssetInventory(request, rng),
            DocumentKind.IncidentReport => IncidentTimeline(Incident(request, rng)),
            DocumentKind.Budget => BudgetLines(request, rng),
            DocumentKind.Personnel => Directory(request),
            DocumentKind.MeetingMinutes => ActionItems(request, rng, Attendees(request, rng)),
            DocumentKind.Policy => Acknowledgements(request, rng),
            DocumentKind.ProjectStatus => Milestones(request, rng, ProjectFor(request, rng)),
            _ => Tracker(request, rng)
        };
    }

    private DocumentSpec NewSpec(ContentRequest request, Person author, string title = null, string subtitle = null) => new()
    {
        Title = title ?? request.Title,
        Subtitle = subtitle ?? $"{world.OrganizationName} | {request.Department}",
        Organization = world.OrganizationName,
        Author = author,
        Date = request.Date
    };

    private static string Pick(Random rng, params string[] options) => options[rng.Next(options.Length)];

    #region Network assessment

    private record FindingTemplate(string Title, string Severity, string Host, string Detail, string Fix);

    private static readonly FindingTemplate[] FindingBank =
    [
        new("Multi-factor authentication is not enforced for remote access", "Critical", "vpn01",
            "Remote users can connect to the VPN with a password alone, so a single reused or guessed password gives access to the internal network.",
            "Require multi-factor authentication for every VPN profile and remove the legacy password-only profile."),
        new("Service account is a member of Domain Admins", "Critical", "dc01",
            "The backup service account holds domain administrator rights and its password has not changed in over three years.",
            "Move the service account to a delegated group with only the rights the backup job needs and rotate its password."),
        new("Default credentials on a management interface", "Critical", "bms01",
            "The building management controller accepts the manufacturer's default administrator password on its web console.",
            "Change the default password, restrict the console to the facilities management VLAN and record the credential in the password vault."),
        new("Operating system is past end of support", "High", "payroll01",
            "The server runs an operating system release that no longer receives security updates from its vendor.",
            "Plan the migration to a supported release before the next payroll year and isolate the server until then."),
        new("Security updates are more than 90 days behind", "High", "fs01",
            "Monthly cumulative updates have not been applied since the last maintenance window was cancelled.",
            "Apply outstanding updates in the next maintenance window and restore the monthly patch schedule."),
        new("Backups have not been test-restored in the past year", "High", "backup01",
            "Backup jobs report success, but no restore test has been recorded, so recovery times are unknown.",
            "Schedule a quarterly restore test of one critical system and record the recovery time achieved."),
        new("Local administrator password is shared across servers", "High", "erp-app01",
            "The same local administrator password is set on most Windows and Linux servers.",
            "Deploy a local administrator password solution so each server has a unique, rotated password."),
        new("SMB signing is not required", "Medium", "fs01",
            "File shares accept unsigned SMB sessions, which allows relay of captured authentication.",
            "Require SMB signing on servers through group policy after confirming that older multifunction printers still connect."),
        new("Database service listens on all interfaces", "Medium", "erp-db01",
            "The ERP database accepts connections from the user network instead of only the application server.",
            "Restrict the listener and host firewall to the ERP application server and the database administration jump host."),
        new("VPN gateway accepts TLS 1.0", "Medium", "vpn01",
            "The gateway still negotiates TLS 1.0 for older clients.",
            "Disable TLS 1.0 and 1.1 once the remaining older clients are upgraded."),
        new("Firewall rule base contains broad inbound rules", "Medium", "fw01",
            "Several inbound rules allow any source to reach internal services and have no recorded owner or business reason.",
            "Review each rule with its system owner, remove unused rules and record an owner and review date for the rest."),
        new("Audit logging is not forwarded centrally", "Medium", "mail01",
            "Security event logs stay on each server and are overwritten within days.",
            "Forward security logs to the central log server and keep them for at least 90 days."),
        new("Internal portal uses a self-signed certificate", "Low", "intranet01",
            "Users are trained to click through certificate warnings when they open the intranet.",
            "Issue a certificate from the internal certificate authority and deploy the root certificate to all endpoints."),
        new("Web server discloses its version", "Low", "helpdesk01",
            "Response headers reveal the exact web server and framework versions.",
            "Remove version banners from response headers."),
        new("Stale user accounts remain enabled", "Low", "dc01",
            "Dozens of accounts for departed staff and contractors have not signed in for over 90 days and are still enabled.",
            "Disable accounts with no sign-in for 90 days and review the list monthly with Human Resources.")
    ];

    private List<(string Id, FindingTemplate Finding, SystemAsset System, string Status)> Findings(ContentRequest request, Random rng)
    {
        var count = rng.Next(7, 12);
        var severityOrder = new Dictionary<string, int> { ["Critical"] = 0, ["High"] = 1, ["Medium"] = 2, ["Low"] = 3 };
        return World.Shuffle(rng, FindingBank).Take(count)
            .OrderBy(f => severityOrder[f.Severity])
            .Select((f, i) => (
                $"F-{request.Date:yy}-{i + 1:00}",
                f,
                world.Systems.FirstOrDefault(s => s.Hostname.StartsWith(f.Host + ".")) ?? world.Systems[0],
                Pick(rng, "Open", "Open", "Open", "In progress", "Accepted risk")))
            .ToList();
    }

    // Each table is drawn first from a fresh seed, both here and in ComposeTable, so a report and
    // the spreadsheet for the same path carry the same rows
    private DocumentSpec NetworkAssessment(ContentRequest request, Random rng)
    {
        var findings = Findings(request, rng);
        var author = world.PersonIn("Information Technology", rng);
        var spec = NewSpec(request, author,
            subtitle: $"{world.OrganizationName} | Information Technology{(request.Period != null ? $" | {request.Period}" : "")}");
        var end = request.Date.AddDays(-rng.Next(3, 10));
        var start = end.AddDays(-rng.Next(5, 15));
        int Count(string severity) => findings.Count(f => f.Finding.Severity == severity);
        var assessor = rng.Next(2) == 0
            ? $"The {world.ShortName} information security team"
            : $"{world.Vendors[0]}, under contract to {world.OrganizationName},";
        var rating = Count("Critical") > 1 ? "high risk" : Count("Critical") == 1 ? "elevated risk" : "moderate risk";

        var summary = new DocumentSection { Heading = "Executive Summary" };
        summary.Paragraphs.Add($"{assessor} assessed the internal network of {world.OrganizationName} between {start:MMMM d} and {end.ToString(LongDate)}. " +
                               $"The assessment covered {world.Systems.Count} servers and network devices on the server, user and remote access networks.");
        summary.Paragraphs.Add($"Overall the environment is rated {rating}. The assessment recorded {findings.Count} findings: " +
                               $"{Count("Critical")} critical, {Count("High")} high, {Count("Medium")} medium and {Count("Low")} low. " +
                               $"The most significant are {Lower(findings[0].Finding.Title)} on {Short(findings[0].System)} and {Lower(findings[1].Finding.Title)} on {Short(findings[1].System)}.");
        summary.Paragraphs.Add("Most findings can be closed with configuration changes and routine patching. " +
                               "Items that need budget or schedule decisions are called out under Recommendations.");
        spec.Sections.Add(summary);

        var scope = new DocumentSection { Heading = "Scope and Methodology" };
        scope.Paragraphs.Add($"All production servers and network devices at the {world.City} headquarters were in scope. " +
                             "Testing was performed during business hours from an internal jump host, with system owners notified in advance. No denial-of-service testing was performed.");
        scope.Bullets.AddRange([
            "Host and service discovery of the server and user networks",
            "Authenticated vulnerability scanning of in-scope servers",
            "Review of firewall and VPN configuration exports",
            "Review of Active Directory privileged groups and service accounts",
            "Interviews with system owners and spot checks of backup records"
        ]);
        spec.Sections.Add(scope);

        var environment = new DocumentSection { Heading = "Environment Overview" };
        environment.Paragraphs.Add($"The server network ({world.Systems[0].IpAddress[..world.Systems[0].IpAddress.LastIndexOf('.')]}.0/24) hosts the systems below. " +
                                   "Each system's owner was confirmed with the department that relies on it.");
        environment.Table = new DataTable
        {
            Name = "Systems",
            Columns = ["System", "Hostname", "IP Address", "Operating System", "Owner"]
        };
        foreach (var system in world.Systems)
            environment.Table.Rows.Add([system.Name, system.Hostname, system.IpAddress, system.OperatingSystem, system.Owner.FullName]);
        spec.Sections.Add(environment);

        var findingsSection = new DocumentSection { Heading = "Findings" };
        findingsSection.Paragraphs.Add("Severity reflects the likely impact on operations and the effort an attacker would need to exploit the issue.");
        findingsSection.Table = new DataTable { Name = "Findings", Columns = ["ID", "Finding", "Severity", "Affected System", "Status"] };
        foreach (var (id, finding, system, status) in findings)
            findingsSection.Table.Rows.Add([id, finding.Title, finding.Severity, Short(system), status]);
        spec.Sections.Add(findingsSection);

        var details = new DocumentSection { Heading = "Finding Details" };
        foreach (var (id, finding, system, _) in findings.Take(4))
            details.Paragraphs.Add($"{id} {finding.Title} ({finding.Severity}). {finding.Detail} Affected: {system.Hostname} ({system.IpAddress}), owned by {system.Owner.FullName}.");
        spec.Sections.Add(details);

        var recommendations = new DocumentSection { Heading = "Recommendations" };
        recommendations.Bullets.AddRange(findings.Take(6).Select(f => $"{f.Id}: {f.Finding.Fix}"));
        recommendations.Paragraphs.Add($"{author.FullName} will track remediation and report progress to {world.HeadOf("Information Technology").FullName}, " +
                                       $"{world.HeadOf("Information Technology").Title}, by {request.Date.AddDays(30).ToString(LongDate)}.");
        spec.Sections.Add(recommendations);
        return spec;
    }

    private DataTable AssetInventory(ContentRequest request, Random rng)
    {
        var findings = Findings(request, rng);
        var table = new DataTable
        {
            Name = "Asset Inventory",
            Columns = ["Hostname", "IP Address", "System", "Operating System", "Role", "Owner", "Department", "Last Patched", "Open Findings"]
        };
        foreach (var system in world.Systems)
        {
            table.Rows.Add([
                system.Hostname, system.IpAddress, system.Name, system.OperatingSystem, system.Role,
                system.Owner.FullName, system.Owner.Department, request.Date.AddDays(-rng.Next(5, 160)),
                findings.Count(f => f.System == system && f.Status != "Accepted risk")
            ]);
        }
        return table;
    }

    private static string Short(SystemAsset system) => system.Hostname.Split('.')[0];

    private static string Initials(string department) =>
        string.Concat(department.Split(' ').Where(w => w != "and").Select(w => w[0]));

    private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text[1..];

    #endregion

    #region Incident report

    private record IncidentTemplate(string Name, string Host, string Symptom, string Cause, string Fix, string[] Services);

    private static readonly IncidentTemplate[] IncidentBank =
    [
        new("Network outage at headquarters", "fw01",
            "Users at headquarters lost access to internal applications and the internet",
            "a firmware fault on a core switch that stopped forwarding traffic after a scheduled configuration backup",
            "The switch was restarted on the previous firmware version and the faulty release was withdrawn from the update schedule",
            ["Email", "ERP", "File shares", "Internet access", "Phones"]),
        new("Email service disruption", "mail01",
            "Email could not be sent or received, and calendars did not update",
            "the mail server's transaction log volume filling up after a failed backup job stopped truncating logs",
            "Logs were truncated after a manual full backup, and disk space alerts were added for the log volume",
            ["Email", "Calendars", "Shared mailboxes"]),
        new("ERP database failure", "erp-db01",
            "Purchasing, invoicing and timekeeping screens returned errors",
            "a storage controller failover that left the database volume read-only",
            "The database was restarted on the healthy controller and storage firmware was updated",
            ["ERP", "Purchasing", "Invoicing", "Timekeeping"]),
        new("Server room power loss", "dc01",
            "Several servers shut down unexpectedly and remote sites lost access to headquarters systems",
            "a failed transfer switch during routine generator testing, which drained the UPS before power returned",
            "Servers were restarted in priority order and the transfer switch was repaired by the facilities contractor",
            ["Authentication", "File shares", "Dispatch", "Phones"]),
        new("Remote access capacity exhaustion", "vpn01",
            "Remote staff could not connect to the VPN during the morning peak",
            "the VPN license limit being reached after a weather closure sent most staff home at once",
            "Temporary licenses were added and the license count was raised for the next renewal",
            ["VPN", "Remote desktop", "Dispatch"]),
        new("Expired certificate on the internal portal", "intranet01",
            "The intranet and document library showed security errors and would not load in most browsers",
            "a certificate that expired because its renewal reminder went to the mailbox of a departed employee",
            "A new certificate was issued and renewal reminders now go to a shared team mailbox",
            ["Intranet", "Document library", "Forms"])
    ];

    private record IncidentFacts(IncidentTemplate Template, SystemAsset System, DateTime Start, DateTime Resolved, Person Lead, List<(DateTime Time, string Event, Person By)> Timeline);

    private IncidentFacts Incident(ContentRequest request, Random rng)
    {
        var template = IncidentBank[rng.Next(IncidentBank.Length)];
        var system = world.Systems.FirstOrDefault(s => s.Hostname.StartsWith(template.Host + ".")) ?? world.Systems[0];
        var start = request.Date.AddDays(-rng.Next(2, 20)).AddHours(rng.Next(6, 15)).AddMinutes(rng.Next(0, 60));
        var detect = start.AddMinutes(rng.Next(4, 25));
        var resolved = detect.AddMinutes(rng.Next(90, 420));
        var lead = world.PersonIn("Information Technology", rng);
        var helpDesk = world.People.FirstOrDefault(p => p.Title == "Help Desk Technician") ?? lead;
        var comms = world.HeadOf("Communications");
        var timeline = new List<(DateTime, string, Person)>
        {
            (start, $"{template.Symptom}.", helpDesk),
            (detect, $"Help desk opened a priority 1 ticket and paged the on-call engineer, {lead.FullName}.", helpDesk),
            (detect.AddMinutes(rng.Next(10, 30)), $"{lead.FirstName} confirmed the problem on {system.Hostname} and opened an incident bridge.", lead),
            (detect.AddMinutes(rng.Next(35, 60)), $"{comms.FullName} sent the first all-staff notice with a workaround and the next update time.", comms),
            (resolved.AddMinutes(-rng.Next(30, 80)), $"Cause identified as {template.Cause}.", lead),
            (resolved, $"Service restored. {template.Fix.Split(',')[0].Split(" and ")[0]}.", lead),
            (resolved.AddMinutes(rng.Next(20, 60)), "Monitoring confirmed normal operation and the incident bridge was closed.", lead)
        };
        return new IncidentFacts(template, system, start, resolved, lead, timeline.OrderBy(e => e.Item1).ToList());
    }

    private DocumentSpec IncidentReport(ContentRequest request, Random rng)
    {
        var incident = Incident(request, rng);
        var t = incident.Template;
        var title = request.Title is "Incident Report" ? $"Incident Report: {t.Name}" : request.Title;
        var spec = NewSpec(request, incident.Lead, title);
        var duration = incident.Resolved - incident.Start;
        var affected = rng.Next(40, 400);

        var summary = new DocumentSection { Heading = "Summary" };
        summary.Paragraphs.Add($"On {incident.Start.ToString(LongDate)} at {incident.Start:h:mm tt}, {Lower(t.Symptom)}. " +
                               $"Service was fully restored at {incident.Resolved:h:mm tt}, {(int)duration.TotalHours} hours and {duration.Minutes} minutes later.");
        summary.Paragraphs.Add($"The cause was {t.Cause}. About {affected} staff were affected. No data was lost.");
        spec.Sections.Add(summary);

        var timeline = new DocumentSection { Heading = "Timeline" };
        timeline.Table = IncidentTimeline(incident);
        spec.Sections.Add(timeline);

        var impact = new DocumentSection { Heading = "Impact" };
        impact.Paragraphs.Add("The following services were unavailable or degraded during the incident:");
        impact.Bullets.AddRange(t.Services);
        impact.Paragraphs.Add($"{world.HeadOf("Operations").FullName} reported that staff used manual workarounds until service was restored. " +
                              "No customer-facing deadlines were missed.");
        spec.Sections.Add(impact);

        var cause = new DocumentSection { Heading = "Root Cause" };
        cause.Paragraphs.Add($"The incident was caused by {t.Cause}. {t.Fix}.");
        spec.Sections.Add(cause);

        var actions = new DocumentSection { Heading = "Corrective Actions" };
        actions.Table = new DataTable { Name = "Corrective Actions", Columns = ["Action", "Owner", "Due", "Status"] };
        actions.Table.Rows.Add([$"Document the recovery steps for {incident.System.Hostname} in the runbook", incident.Lead.FullName, request.Date.AddDays(14), "Open"]);
        actions.Table.Rows.Add(["Add monitoring for the condition that caused this incident", world.PersonIn("Information Technology", rng).FullName, request.Date.AddDays(21), "In progress"]);
        actions.Table.Rows.Add(["Review the all-staff notification templates", world.HeadOf("Communications").FullName, request.Date.AddDays(30), "Open"]);
        actions.Table.Rows.Add(["Include this scenario in the next disaster recovery exercise", world.HeadOf("Information Technology").FullName, request.Date.AddDays(60), "Open"]);
        spec.Sections.Add(actions);

        var lessons = new DocumentSection { Heading = "Lessons Learned" };
        lessons.Bullets.AddRange([
            "The help desk recognized the pattern quickly because of the priority 1 checklist.",
            "The first status update took too long; a pre-approved message would have saved time.",
            $"Only {incident.Lead.FirstName} knew the recovery steps for {Short(incident.System)}, which made the on-call rotation a single point of failure."
        ]);
        spec.Sections.Add(lessons);
        return spec;
    }

    private static DataTable IncidentTimeline(IncidentFacts incident)
    {
        var table = new DataTable { Name = "Timeline", Columns = ["Time", "Event", "Recorded By"] };
        foreach (var (time, text, by) in incident.Timeline)
            table.Rows.Add([time.ToString("yyyy-MM-dd HH:mm"), text, by.FullName]);
        return table;
    }

    #endregion

    #region Budget

    private static readonly (string Category, int Low, int High)[] BudgetCategories =
    [
        ("Salaries and Benefits", 800_000, 2_400_000), ("Software Licenses", 60_000, 240_000), ("Hardware", 40_000, 180_000),
        ("Contracted Services", 50_000, 300_000), ("Training", 8_000, 40_000), ("Travel", 5_000, 30_000),
        ("Facilities and Utilities", 90_000, 320_000), ("Telecommunications", 20_000, 90_000), ("Office Supplies", 4_000, 18_000)
    ];

    private DataTable BudgetLines(ContentRequest request, Random rng)
    {
        var table = new DataTable
        {
            Name = "Budget vs Actual",
            Columns = ["Category", "Owner", "Budget", "Actual", "Variance"],
            CurrencyColumns = ["Budget", "Actual", "Variance"],
            HasTotals = true
        };
        // a quarter is a quarter of the annual figures
        var scale = request.Quarter.HasValue ? 0.25m : 1m;
        foreach (var (category, low, high) in BudgetCategories)
        {
            var budget = Math.Round(rng.Next(low, high) * scale / 100m) * 100m;
            var actual = Math.Round(budget * (decimal)(0.82 + rng.NextDouble() * 0.3), 2);
            var owner = category switch
            {
                "Salaries and Benefits" => world.HeadOf("Human Resources"),
                "Software Licenses" or "Hardware" or "Telecommunications" => world.HeadOf("Information Technology"),
                "Facilities and Utilities" => world.HeadOf("Facilities"),
                _ => world.HeadOf(request.Department)
            };
            table.Rows.Add([category, owner.FullName, budget, actual, new Computed(budget - actual, "=C{r}-D{r}")]);
        }
        return table;
    }

    private DocumentSpec Budget(ContentRequest request, Random rng)
    {
        var author = world.People.FirstOrDefault(p => p.Title == "Financial Analyst") ?? world.HeadOf("Finance");
        var period = request.Period ?? $"FY{request.Date.Year}";
        var title = request.Title.Contains(period.Split(' ')[0]) ? request.Title : $"{request.Title}, {period}";
        var spec = NewSpec(request, author, title);
        var table = BudgetLines(request, rng);
        var budget = table.Rows.Sum(r => (decimal)r[2]);
        var actual = table.Rows.Sum(r => (decimal)r[3]);
        var used = actual / budget;

        var summary = new DocumentSection { Heading = "Summary" };
        summary.Paragraphs.Add($"For {period}, {world.OrganizationName} spent {Money(actual)} against a budget of {Money(budget)}, " +
                               $"{Percent(used)} of plan. The net variance is {(budget - actual >= 0 ? "a favorable" : "an unfavorable")} {Money(Math.Abs(budget - actual))}.");
        summary.Paragraphs.Add("Figures are drawn from the general ledger as of the close date and exclude encumbrances not yet invoiced.");
        spec.Sections.Add(summary);

        spec.Sections.Add(new DocumentSection { Heading = "Budget vs Actual", Table = table });

        var commentary = new DocumentSection { Heading = "Variance Commentary" };
        foreach (var row in table.Rows.OrderBy(r => ((Computed)r[4]).Value).Take(3))
        {
            var variance = ((Computed)row[4]).Value;
            var share = variance / (decimal)row[2];
            commentary.Bullets.Add(variance < 0
                ? $"{row[0]} is {Percent(Math.Abs(share))} over budget ({Money(Math.Abs(variance))}), mainly from {Pick(rng, $"{world.Vendors[rng.Next(world.Vendors.Count)]} renewals", "unplanned replacements", "overtime during the outage response", "timing of annual invoices")}."
                : $"{row[0]} is {Percent(share)} under budget ({Money(variance)}) because {Pick(rng, "open positions were filled late", "purchases moved to next period", "a contract came in below estimate")}.");
        }
        spec.Sections.Add(commentary);

        var outlook = new DocumentSection { Heading = "Outlook" };
        outlook.Paragraphs.Add($"Finance expects year-end spending within {rng.Next(1, 4)}% of the approved budget. " +
                               $"Departments over plan should send revised forecasts to {author.FullName} by {request.Date.AddDays(15).ToString(LongDate)}.");
        outlook.Paragraphs.Add($"Reviewed by {world.HeadOf("Finance").FullName}, {world.HeadOf("Finance").Title}.");
        spec.Sections.Add(outlook);
        return spec;
    }

    #endregion

    #region Personnel

    private List<Person> PeopleFor(ContentRequest request) =>
        request.Department == "Human Resources" ? world.People.ToList() : world.People.Where(p => p.Department == request.Department).ToList();

    private DataTable Directory(ContentRequest request)
    {
        var rng = request.CreateRandom(1);
        var table = new DataTable
        {
            Name = "Directory",
            Columns = ["Name", "Title", "Department", "Email", "Phone", "Username", "Start Date"]
        };
        foreach (var person in PeopleFor(request))
            table.Rows.Add([person.FullName, person.Title, person.Department, person.Email, person.Phone, person.Username,
                world.ReferenceDate.AddDays(-rng.Next(60, 365 * 15))]);
        return table;
    }

    private DocumentSpec Personnel(ContentRequest request, Random rng)
    {
        var author = world.People.FirstOrDefault(p => p.Title == "HR Business Partner") ?? world.HeadOf("Human Resources");
        var spec = NewSpec(request, author);
        var people = PeopleFor(request);

        var overview = new DocumentSection { Heading = "Overview" };
        overview.Paragraphs.Add($"This directory lists the {people.Count} staff of {(request.Department == "Human Resources" ? world.OrganizationName : $"the {request.Department} department")} " +
                                $"as of {request.Date.ToString(LongDate)}. It is for internal use and must not be shared outside {world.ShortName}.");
        spec.Sections.Add(overview);

        var headcount = new DocumentSection
        {
            Heading = "Headcount by Department",
            Table = new DataTable { Name = "Headcount", Columns = ["Department", "Department Head", "Staff"] }
        };
        foreach (var group in people.GroupBy(p => p.Department))
            headcount.Table.Rows.Add([group.Key, world.HeadOf(group.Key).FullName, group.Count()]);
        spec.Sections.Add(headcount);

        var directory = new DocumentSection
        {
            Heading = "Staff",
            Table = new DataTable { Name = "Staff", Columns = ["Name", "Title", "Department", "Email", "Phone"] }
        };
        foreach (var person in people)
            directory.Table.Rows.Add([person.FullName, person.Title, person.Department, person.Email, person.Phone]);
        spec.Sections.Add(directory);

        var notes = new DocumentSection { Heading = "Changes" };
        notes.Paragraphs.Add($"Send corrections and new-hire details to {author.FullName} ({author.Email}). " +
                             "Changes are applied to the directory, email distribution lists and the phone system together.");
        spec.Sections.Add(notes);
        return spec;
    }

    #endregion

    #region Meeting minutes

    private List<Person> Attendees(ContentRequest request, Random rng)
    {
        var chair = world.HeadOf(request.Department);
        var others = World.Shuffle(rng, world.People.Where(p => p != chair && (p.Department == request.Department || rng.Next(4) == 0))).Take(rng.Next(4, 7));
        return [chair, .. others];
    }

    private DataTable ActionItems(ContentRequest request, Random rng, List<Person> attendees)
    {
        var table = new DataTable { Name = "Action Items", Columns = ["Action", "Owner", "Due Date", "Status"] };
        var actions = new[]
        {
            $"Circulate the revised schedule for {world.Projects[0]}",
            $"Confirm the maintenance window for {Short(world.Systems[rng.Next(world.Systems.Count)])} with affected departments",
            "Send the updated budget forecast to Finance",
            $"Schedule a walkthrough with {world.Vendors[rng.Next(world.Vendors.Count)]}",
            "Update the on-call rotation for the next quarter",
            "Draft the staff notice for the office move"
        };
        foreach (var action in World.Shuffle(rng, actions).Take(rng.Next(3, 6)))
            table.Rows.Add([action, attendees[rng.Next(attendees.Count)].FullName, request.Date.AddDays(rng.Next(5, 30)), Pick(rng, "Open", "Open", "In progress")]);
        return table;
    }

    private DocumentSpec MeetingMinutes(ContentRequest request, Random rng)
    {
        var attendees = Attendees(request, rng);
        var actionItems = ActionItems(request, rng, attendees);
        var chair = attendees[0];
        var scribe = attendees[^1];
        var title = request.Title is "Meeting Minutes" ? $"{request.Department} Staff Meeting Minutes" : request.Title;
        var meetingDate = request.Date.AddDays(-rng.Next(0, 6));
        var hour = rng.Next(9, 15);
        var spec = NewSpec(request, scribe, title, $"{world.OrganizationName} | {meetingDate.ToString(LongDate)}, {hour}:00 | {Pick(rng, "Conference Room B-204", "Board Room", "Teams call", "Conference Room A-110")}");

        var present = new DocumentSection { Heading = "Attendees" };
        present.Bullets.AddRange(attendees.Select((p, i) => $"{p.FullName}, {p.Title}{(i == 0 ? " (chair)" : p == scribe ? " (minutes)" : "")}"));
        spec.Sections.Add(present);

        var project = world.Projects[rng.Next(world.Projects.Count)];
        var system = world.Systems[rng.Next(world.Systems.Count)];
        var agenda = new DocumentSection { Heading = "Agenda" };
        agenda.Bullets.AddRange(["Review of previous action items", $"{project} update", $"Planned maintenance on {Short(system)}", "Budget and staffing", "Other business"]);
        spec.Sections.Add(agenda);

        var discussion = new DocumentSection { Heading = "Discussion" };
        discussion.Paragraphs.Add($"{chair.FirstName} opened the meeting at {hour}:00. All but {rng.Next(1, 3)} open action items from the last meeting are complete.");
        discussion.Paragraphs.Add($"{project}: {attendees[1 % attendees.Count].FullName} reported the work is {Pick(rng, "on schedule", "about two weeks behind", "slightly ahead of plan")}. " +
                                  $"{Pick(rng, "Testing starts next month.", "The vendor has asked for an extra week to finish configuration.", "User training is being scheduled.")}");
        discussion.Paragraphs.Add($"Maintenance on {system.Name} ({system.Hostname}) is planned for a weekend evening. {system.Owner.FullName} will confirm the date with the departments that use it.");
        discussion.Paragraphs.Add($"Budget and staffing: {Pick(rng, "one open position is expected to be filled this month", "overtime is above plan because of recent incidents", "training spending is under plan")}. No decisions were needed.");
        spec.Sections.Add(discussion);

        spec.Sections.Add(new DocumentSection { Heading = "Action Items", Table = actionItems });

        var next = new DocumentSection { Heading = "Next Meeting" };
        next.Paragraphs.Add($"The next meeting is on {meetingDate.AddDays(14).ToString(LongDate)}. Minutes recorded by {scribe.FullName}.");
        spec.Sections.Add(next);
        return spec;
    }

    #endregion

    #region Policy

    private static readonly Dictionary<string, string[]> PolicyStatements = new()
    {
        ["password"] =
        [
            "Passwords must be at least 14 characters long and must not reuse any of the previous ten passwords.",
            "Multi-factor authentication is required for email, remote access and every administrative account.",
            "Passwords must never be shared, written down near a workstation or sent by email or chat.",
            "Service account passwords are stored in the password vault and rotated at least once a year.",
            "Suspected password exposure must be reported to the IT help desk immediately."
        ],
        ["remote"] =
        [
            "Remote work requires the approval of the employee's manager and a signed remote work agreement.",
            "Company data may only be accessed remotely through the VPN on a company-managed device.",
            "Employees must work from a private location where screens and calls cannot be overheard.",
            "Printed company documents must not be left at a remote location and must be shredded when no longer needed.",
            "Lost or stolen devices must be reported to the IT help desk within one hour."
        ],
        ["retention"] =
        [
            "Records are kept for the periods in the records retention schedule and then destroyed securely.",
            "Records subject to a legal hold must not be destroyed until Legal and Compliance releases the hold.",
            "Email is not a records system; records received by email must be filed in the document library.",
            "Department records coordinators review their records against the schedule every year."
        ],
        ["acceptable"] =
        [
            "Company systems are provided for business use; limited personal use is allowed if it does not interfere with work.",
            "Users must not install unapproved software or connect personal devices to the internal network.",
            "Activity on company systems may be monitored and logged in line with applicable law.",
            "Users must lock their workstation when leaving it unattended."
        ],
        ["travel"] =
        [
            "Business travel must be approved in advance by the traveler's manager.",
            "Travelers must book through the approved travel agency and choose the lowest reasonable fare.",
            "Expense reports with itemized receipts are due within 30 days of returning.",
            "Laptops and phones must be carried, never checked, when traveling."
        ]
    };

    private static readonly string[] GenericStatements =
    [
        "This policy applies to every employee, contractor and volunteer of the organization.",
        "Managers are responsible for making sure their staff have read and understood this policy.",
        "Exceptions must be requested in writing and approved by the policy owner.",
        "Records needed to show compliance with this policy are kept for at least three years.",
        "Questions about this policy should be directed to the policy owner."
    ];

    private DocumentSpec Policy(ContentRequest request, Random rng)
    {
        var owner = world.HeadOf(request.Department);
        var title = request.Title.Contains("Policy", StringComparison.OrdinalIgnoreCase) || request.Title.Contains("Procedure", StringComparison.OrdinalIgnoreCase)
            ? request.Title
            : $"{request.Title} Policy";
        var key = PolicyStatements.Keys.FirstOrDefault(k => request.Path.Contains(k, StringComparison.OrdinalIgnoreCase));
        var topic = title.Replace(" Policy", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var spec = NewSpec(request, owner, title, $"{world.OrganizationName} | Policy {Initials(request.Department)}-{rng.Next(100, 400)} | Effective {request.Date.AddMonths(-rng.Next(1, 18)).ToString(LongDate)}");

        var purpose = new DocumentSection { Heading = "1. Purpose" };
        purpose.Paragraphs.Add($"This policy sets out how {world.OrganizationName} manages {topic}, so staff know what is expected of them and the organization meets its legal and contractual obligations.");
        spec.Sections.Add(purpose);

        var scope = new DocumentSection { Heading = "2. Scope" };
        scope.Paragraphs.Add($"This policy applies to all {world.ShortName} employees, contractors and volunteers, and to all company systems and records, wherever they are used.");
        spec.Sections.Add(scope);

        var statements = new DocumentSection { Heading = "3. Policy" };
        statements.Bullets.AddRange(key != null ? PolicyStatements[key] : GenericStatements.Take(3));
        spec.Sections.Add(statements);

        var responsibilities = new DocumentSection { Heading = "4. Responsibilities" };
        responsibilities.Bullets.AddRange([
            $"Policy owner: {owner.FullName}, {owner.Title}",
            "Managers: make sure their staff follow this policy and approve exceptions only within their authority",
            $"{world.HeadOf("Information Technology").Title}: provide and maintain the technical controls this policy relies on",
            "All staff: follow this policy and report concerns"
        ]);
        spec.Sections.Add(responsibilities);

        var compliance = new DocumentSection { Heading = "5. Compliance" };
        compliance.Paragraphs.Add("Failure to follow this policy may result in loss of system access and disciplinary action up to and including termination. " +
                                  "Legal and Compliance reviews adherence to this policy as part of the annual internal audit plan.");
        spec.Sections.Add(compliance);

        var history = new DocumentSection
        {
            Heading = "6. Revision History",
            Table = new DataTable { Name = "Revision History", Columns = ["Version", "Date", "Author", "Change"] }
        };
        var versions = rng.Next(2, 5);
        for (var v = 1; v <= versions; v++)
        {
            history.Table.Rows.Add([$"{v}.0", request.Date.AddMonths(-(versions - v) * rng.Next(6, 14) - 1),
                (v == versions ? owner : world.PersonIn(request.Department, rng)).FullName,
                v == 1 ? "Initial release" : Pick(rng, "Annual review, no material changes", "Updated responsibilities", "Aligned with new system controls", "Clarified exceptions process")]);
        }
        spec.Sections.Add(history);
        return spec;
    }

    private DataTable Acknowledgements(ContentRequest request, Random rng)
    {
        var table = new DataTable { Name = "Acknowledgements", Columns = ["Employee", "Department", "Version", "Acknowledged On"] };
        foreach (var person in PeopleFor(request).Where(_ => rng.Next(5) > 0))
            table.Rows.Add([person.FullName, person.Department, "2.0", request.Date.AddDays(-rng.Next(1, 90))]);
        return table;
    }

    #endregion

    #region Project status

    private string ProjectFor(ContentRequest request, Random rng) =>
        world.Projects.FirstOrDefault(p => request.Title.Contains(p, StringComparison.OrdinalIgnoreCase)
                                           || p.Split(' ').Any(w => w.Length > 3 && request.Path.Contains(w, StringComparison.OrdinalIgnoreCase)))
        ?? world.Projects[rng.Next(world.Projects.Count)];

    private DataTable Milestones(ContentRequest request, Random rng, string project)
    {
        var table = new DataTable { Name = "Milestones", Columns = ["Milestone", "Owner", "Planned", "Forecast", "Status"] };
        var names = new[] { "Requirements approved", "Vendor selected", "Design complete", "Build complete", "User acceptance testing", "Go-live", "Project closeout" };
        var start = request.Date.AddMonths(-rng.Next(4, 9));
        for (var i = 0; i < names.Length; i++)
        {
            var planned = start.AddDays(i * rng.Next(25, 45));
            var slip = rng.Next(3) == 0 ? rng.Next(5, 30) : 0;
            var forecast = planned.AddDays(slip);
            var status = forecast < request.Date ? "Complete" : planned < request.Date.AddDays(30) ? (slip > 0 ? "At risk" : "On track") : "Not started";
            table.Rows.Add([names[i], world.PersonIn(request.Department, rng).FullName, planned, forecast, status]);
        }
        return table;
    }

    private DocumentSpec ProjectStatus(ContentRequest request, Random rng)
    {
        var project = ProjectFor(request, rng);
        var milestones = Milestones(request, rng, project);
        var manager = world.PersonIn(request.Department, rng);
        var title = request.Title is "Project Status Report" ? $"{project}: Status Report" : request.Title;
        var spec = NewSpec(request, manager, title);
        var complete = milestones.Rows.Count(r => (string)r[4] == "Complete");
        var atRisk = milestones.Rows.Count(r => (string)r[4] == "At risk");
        var status = atRisk > 1 ? "Red" : atRisk == 1 ? "Amber" : "Green";
        var budget = rng.Next(150, 900) * 1000m;
        var spent = budget * (decimal)(0.2 + rng.NextDouble() * 0.6);

        var summary = new DocumentSection { Heading = "Summary" };
        summary.Paragraphs.Add($"Overall status: {status}. {project} has completed {complete} of {milestones.Rows.Count} milestones as of {request.Date.ToString(LongDate)}. " +
                               $"{Money(spent)} of the {Money(budget)} budget has been spent.");
        summary.Paragraphs.Add(atRisk > 0
            ? $"{atRisk} milestone{(atRisk > 1 ? "s are" : " is")} at risk because {Pick(rng, $"{world.Vendors[rng.Next(world.Vendors.Count)]} delivered late", "key staff were pulled into incident response", "testing found more defects than expected")}. The project sponsor has been informed."
            : "All remaining milestones are on track and no decisions are needed from the steering group this period.");
        spec.Sections.Add(summary);

        spec.Sections.Add(new DocumentSection { Heading = "Milestones", Table = milestones });

        var risks = new DocumentSection
        {
            Heading = "Risks",
            Table = new DataTable { Name = "Risks", Columns = ["Risk", "Likelihood", "Impact", "Mitigation", "Owner"] }
        };
        risks.Table.Rows.Add(["Key staff unavailable during cutover", Pick(rng, "Medium", "High"), "High", "Cross-train a second engineer and fix the cutover date early", manager.FullName]);
        risks.Table.Rows.Add(["Vendor delivery slips", Pick(rng, "Low", "Medium"), "Medium", "Weekly vendor check-in with escalation path", world.HeadOf("Finance").FullName]);
        risks.Table.Rows.Add(["Users not ready for the change", "Medium", "Medium", "Training sessions and a help desk quick-reference guide", world.HeadOf("Communications").FullName]);
        spec.Sections.Add(risks);

        var next = new DocumentSection { Heading = "Next Steps" };
        next.Bullets.AddRange(milestones.Rows.Where(r => (string)r[4] != "Complete").Take(3).Select(r => $"{r[0]}: forecast {(DateTime)r[3]:MMMM d}, owner {r[1]}"));
        spec.Sections.Add(next);
        return spec;
    }

    #endregion

    #region Memo

    private DocumentSpec Memo(ContentRequest request, Random rng)
    {
        var author = world.HeadOf(request.Department);
        var audience = request.Department is "Executive Office" ? "All Staff" : $"{request.Department} Staff";
        var system = world.Systems[rng.Next(world.Systems.Count)];
        var project = world.Projects[rng.Next(world.Projects.Count)];
        var helpDesk = world.People.FirstOrDefault(p => p.Title == "Help Desk Technician") ?? world.HeadOf("Information Technology");

        (string Subject, string Opening, string Detail)[] variants =
        [
            ("Planned System Maintenance",
                $"As part of {project}, {system.Name} ({system.Hostname}) will be unavailable on {request.Date.AddDays(rng.Next(3, 12)):dddd, MMMM d} from 6:00 PM to 10:00 PM while it is upgraded.",
                "Please save your work and sign out before the window starts. Anything left open may be lost."),
            ($"{project} Update",
                $"I want to give everyone an update on {project} and what it means for your work over the next few weeks.",
                "The team finished the first phase on schedule. The next phase begins after the quarter closes, and affected teams will be contacted directly."),
            ("Reminder: Annual Security Training",
                $"Annual security awareness training must be completed by {request.Date.AddDays(rng.Next(10, 30)).ToString(LongDate)}.",
                "The course takes about 45 minutes and can be paused and resumed. Managers receive a completion report each week.")
        ];
        var variant = variants[rng.Next(variants.Length)];
        if (request.Title is not "Memorandum")
        {
            variant = (request.Title,
                $"This memo updates {audience.ToLowerInvariant()} on {request.Title.ToLowerInvariant()}.",
                "Details will be covered at the next staff meeting. Until then, your manager can answer questions.");
        }

        var spec = NewSpec(request, author, variant.Subject, $"MEMORANDUM | To: {audience} | From: {author.FullName}, {author.Title} | {request.Date.ToString(LongDate)}");
        var body = new DocumentSection { Heading = variant.Subject };
        body.Paragraphs.Add(variant.Opening);
        body.Paragraphs.Add(variant.Detail);
        body.Paragraphs.Add($"If you have questions, contact {helpDesk.FullName} at the IT help desk ({helpDesk.Phone}) or reply to this memo.");
        body.Paragraphs.Add($"{author.FullName}, {author.Title}");
        spec.Sections.Add(body);
        return spec;
    }

    private DataTable Tracker(ContentRequest request, Random rng)
    {
        var table = new DataTable { Name = "Tracker", Columns = ["Item", "Owner", "Department", "Due Date", "Priority", "Status"] };
        var items = new[]
        {
            $"Renew contract with {world.Vendors[0]}", $"Patch {Short(world.Systems[3])}", $"Kickoff for {world.Projects[1]}",
            "Update the emergency contact list", "Review open purchase orders", "Plan the quarterly all-hands meeting",
            $"Retire {Short(world.Systems[6])} after migration", "Collect training completion reports", "Refresh conference room equipment"
        };
        foreach (var item in items)
        {
            var owner = world.People[rng.Next(world.People.Count)];
            table.Rows.Add([item, owner.FullName, owner.Department, request.Date.AddDays(rng.Next(-10, 45)),
                Pick(rng, "High", "Medium", "Medium", "Low"), Pick(rng, "Open", "In progress", "Done", "Blocked")]);
        }
        return table;
    }

    #endregion
}
