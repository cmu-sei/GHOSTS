using System.Globalization;
using System.Text.RegularExpressions;

namespace Ghosts.Pandora.Infrastructure.Content;

public enum DocumentKind
{
    Memo,
    NetworkAssessment,
    IncidentReport,
    Budget,
    Personnel,
    MeetingMinutes,
    Policy,
    ProjectStatus
}

/// <summary>
/// What a request path asks for: "/reports/2026/q3/network-assessment.docx" is a network
/// assessment for Q3 2026 by IT. The seed comes from the path without its extension, so
/// budget.xlsx, budget.pdf and budget.csv in the same folder carry the same figures.
/// </summary>
public class ContentRequest
{
    public string Path { get; private init; }
    public string Title { get; private init; }
    public DocumentKind Kind { get; private init; }
    public string Department { get; private init; }
    public int? Year { get; private init; }
    public int? Quarter { get; private init; }
    public int Seed { get; private init; }

    /// <summary>The date the document is "as of": the end of its quarter or year, else the world's reference date.</summary>
    public DateTime Date { get; private init; }

    public string Period => Quarter.HasValue ? $"Q{Quarter} {Year}" : Year?.ToString(CultureInfo.InvariantCulture);

    public Random CreateRandom(int salt = 0) => new(unchecked(Seed * 31 + salt));

    private static readonly (DocumentKind Kind, string[] Words)[] KindWords =
    [
        (DocumentKind.IncidentReport, ["incident", "outage", "breach", "postmortem", "aar", "disruption", "downtime"]),
        (DocumentKind.NetworkAssessment, ["network", "assessment", "vulnerability", "vulnerabilities", "vuln", "pentest", "penetration", "scan", "security", "firewall", "topology", "infrastructure", "assets", "inventory", "hosts"]),
        (DocumentKind.Budget, ["budget", "budgets", "finance", "financial", "financials", "expense", "expenses", "revenue", "forecast", "spend", "spending", "costs", "invoice", "invoices", "ledger"]),
        (DocumentKind.Personnel, ["personnel", "hr", "employee", "employees", "staff", "roster", "directory", "people", "headcount", "contacts", "users", "team", "org", "orgchart"]),
        (DocumentKind.MeetingMinutes, ["minutes", "meeting", "meetings", "agenda", "standup"]),
        (DocumentKind.Policy, ["policy", "policies", "procedure", "procedures", "handbook", "guideline", "guidelines", "standard", "standards", "sop"]),
        (DocumentKind.ProjectStatus, ["project", "projects", "status", "roadmap", "plan", "proposal", "milestones", "timeline"]),
        (DocumentKind.Memo, ["memo", "memos", "letter", "notice", "announcement"])
    ];

    private static readonly Dictionary<string, string> DepartmentWords = new()
    {
        ["finance"] = "Finance", ["accounting"] = "Finance", ["budget"] = "Finance", ["payroll"] = "Finance",
        ["hr"] = "Human Resources", ["personnel"] = "Human Resources", ["people"] = "Human Resources", ["employees"] = "Human Resources",
        ["it"] = "Information Technology", ["tech"] = "Information Technology", ["security"] = "Information Technology", ["network"] = "Information Technology",
        ["ops"] = "Operations", ["operations"] = "Operations", ["field"] = "Operations",
        ["legal"] = "Legal and Compliance", ["compliance"] = "Legal and Compliance",
        ["communications"] = "Communications", ["comms"] = "Communications", ["marketing"] = "Communications", ["pr"] = "Communications",
        ["facilities"] = "Facilities", ["maintenance"] = "Facilities",
        ["executive"] = "Executive Office", ["board"] = "Executive Office", ["leadership"] = "Executive Office"
    };

    private static readonly Dictionary<DocumentKind, string> KindDepartments = new()
    {
        [DocumentKind.NetworkAssessment] = "Information Technology",
        [DocumentKind.IncidentReport] = "Information Technology",
        [DocumentKind.Budget] = "Finance",
        [DocumentKind.Personnel] = "Human Resources",
        [DocumentKind.MeetingMinutes] = "Executive Office",
        [DocumentKind.Policy] = "Legal and Compliance",
        [DocumentKind.ProjectStatus] = "Operations",
        [DocumentKind.Memo] = "Executive Office"
    };

    private static readonly Dictionary<DocumentKind, string> KindTitles = new()
    {
        [DocumentKind.NetworkAssessment] = "Network Security Assessment",
        [DocumentKind.IncidentReport] = "Incident Report",
        [DocumentKind.Budget] = "Budget Report",
        [DocumentKind.Personnel] = "Staff Directory",
        [DocumentKind.MeetingMinutes] = "Meeting Minutes",
        [DocumentKind.Policy] = "Policy",
        [DocumentKind.ProjectStatus] = "Project Status Report",
        [DocumentKind.Memo] = "Memorandum"
    };

    // Words that say nothing about the document, so a file named like this gets the kind's title
    private static readonly HashSet<string> GenericNames =
        ["index", "file", "document", "doc", "report", "data", "download", "untitled", "default", "new", "export", "copy", "final", "draft"];

    private static readonly HashSet<string> UpperWords = ["it", "hr", "q1", "q2", "q3", "q4", "fy", "vpn", "erp", "sop", "aar", "pr", "ceo", "cfo", "cio", "usa", "api"];

    private static readonly HashSet<string> SmallWords = ["a", "an", "and", "for", "in", "of", "on", "or", "the", "to", "with"];

    public static ContentRequest Parse(string path, World world)
    {
        path = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileName = segments.Length > 0 ? segments[^1] : string.Empty;
        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var folders = segments.Length > 1 ? segments[..^1] : [];

        var nameWords = Words(stem);
        var folderWords = folders.SelectMany(Words).ToList();
        var allWords = nameWords.Concat(folderWords).ToList();

        // the file name is more specific than its folders, and a folder more specific than its parents
        var kind = KindFor(nameWords)
                   ?? Enumerable.Reverse(folders).Select(f => KindFor(Words(f))).FirstOrDefault(k => k.HasValue)
                   ?? DocumentKind.Memo;

        var department = folderWords.Concat(nameWords).Select(w => DepartmentWords.GetValueOrDefault(w)).FirstOrDefault(d => d != null)
                         ?? KindDepartments[kind];

        int? year = allWords.Select(YearOf).FirstOrDefault(y => y.HasValue);
        int? quarter = allWords.Select(QuarterOf).FirstOrDefault(q => q.HasValue);
        if (quarter.HasValue)
            year ??= world.ReferenceDate.Year;

        var date = quarter.HasValue
            ? new DateTime(year!.Value, quarter.Value * 3, 1).AddMonths(1).AddDays(-1)
            : year.HasValue && year.Value < world.ReferenceDate.Year
                ? new DateTime(year.Value, 12, 31)
                : world.ReferenceDate;
        if (date > world.ReferenceDate)
            date = world.ReferenceDate;

        var titleWords = nameWords.Where(w => YearOf(w) == null && QuarterOf(w) == null && !w.All(char.IsDigit)).ToList();
        var title = titleWords.Count == 0 || titleWords.All(GenericNames.Contains)
            ? KindTitles[kind]
            : TitleCase(titleWords);

        var seedPath = segments.Length > 0
            ? string.Join('/', folders.Append(stem)).ToLowerInvariant()
            : string.Empty;

        return new ContentRequest
        {
            Path = path,
            Title = title,
            Kind = kind,
            Department = department,
            Year = year,
            Quarter = quarter,
            Date = date,
            Seed = unchecked((int)StableHash(seedPath) ^ world.Seed)
        };
    }

    /// <summary>FNV-1a, because string.GetHashCode differs between processes.</summary>
    public static uint StableHash(string value)
    {
        var hash = 2166136261;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    /// <summary>
    /// The kind named by the last word that names one, since the noun at the end usually says
    /// what a file is: "staff-meeting" is minutes, "budget-meeting" too, "network-assessment" an assessment.
    /// </summary>
    private static DocumentKind? KindFor(IEnumerable<string> words)
    {
        foreach (var word in words.Reverse())
        {
            foreach (var (kind, kindWords) in KindWords)
            {
                if (kindWords.Contains(word))
                    return kind;
            }
        }
        return null;
    }

    private static int? YearOf(string word)
    {
        var digits = word.StartsWith("fy") ? word[2..] : word;
        return digits.Length == 4 && int.TryParse(digits, out var year) && year is >= 1990 and <= 2100 ? year : null;
    }

    private static int? QuarterOf(string word) =>
        Regex.Match(word, "^q([1-4])$") is { Success: true } m ? int.Parse(m.Groups[1].Value) : null;

    /// <summary>Splits "network-assessment", "network_assessment" and "NetworkAssessment" into lower-case words.</summary>
    private static List<string> Words(string value) =>
        Regex.Matches(Regex.Replace(value ?? string.Empty, "([a-z])([A-Z])", "$1 $2"), "[A-Za-z]+[0-9]*|[0-9]+")
            .Select(m => m.Value.ToLowerInvariant())
            .ToList();

    private static string TitleCase(List<string> words) =>
        string.Join(' ', words.Select((w, i) =>
            UpperWords.Contains(w) ? w.ToUpperInvariant()
            : i > 0 && SmallWords.Contains(w) ? w
            : char.ToUpperInvariant(w[0]) + w[1..]));
}
