using System.Text.Json;

namespace Ghosts.Pandora.Infrastructure.Content;

public record Person(string FirstName, string LastName, string Title, string Department, string Email, string Phone, string Username)
{
    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>An internal system of the organization, so assessments, inventories and incidents name the same hosts.</summary>
public record SystemAsset(string Name, string Hostname, string IpAddress, string OperatingSystem, string Role, Person Owner);

/// <summary>
/// The one fictional organization every generated document, dataset and website page is about.
/// It is derived from a seed, so the same seed always gives the same people, systems and projects.
/// The seed and the reference date are kept in the content directory, so an unconfigured server
/// keeps its world across restarts; deleting that directory resets it.
/// </summary>
public class World
{
    public int Seed { get; }
    public DateTime ReferenceDate { get; }
    public string OrganizationName { get; }
    public string ShortName { get; }
    public string Domain { get; }
    public string City { get; }
    public string Industry { get; }
    public IReadOnlyList<string> Departments { get; }
    public IReadOnlyList<Person> People { get; }
    public IReadOnlyList<SystemAsset> Systems { get; }
    public IReadOnlyList<string> Projects { get; }
    public IReadOnlyList<string> Vendors { get; }

    public const string StateFileName = "world.json";

    private static readonly string[] FirstNames =
    [
        "James", "Maria", "Robert", "Linda", "Michael", "Patricia", "David", "Jennifer", "William", "Elizabeth",
        "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Daniel", "Karen", "Matthew", "Nancy",
        "Anthony", "Lisa", "Mark", "Angela", "Steven", "Michelle", "Andrew", "Laura", "Kevin", "Emily",
        "Brian", "Rachel", "Jason", "Olivia", "Ryan", "Hannah", "Eric", "Megan", "Adam", "Rebecca",
        "Samuel", "Grace", "Nathan", "Priya", "Carlos", "Mei", "Omar", "Fatima", "Diego", "Aisha",
        "Victor", "Elena", "Hector", "Naomi", "Raj", "Sofia", "Tyrone", "Keiko", "Marcus", "Ingrid"
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee",
        "Thompson", "White", "Harris", "Clark", "Lewis", "Robinson", "Walker", "Young", "Allen", "King",
        "Wright", "Scott", "Nguyen", "Hill", "Flores", "Green", "Adams", "Nelson", "Baker", "Hall",
        "Campbell", "Mitchell", "Carter", "Roberts", "Patel", "Kim", "Chen", "Okafor", "Novak", "Fischer",
        "Sullivan", "Murphy", "Reyes", "Morgan", "Cooper", "Price", "Bennett", "Ward", "Foster", "Hughes"
    ];

    private static readonly string[] Prefixes =
    [
        "Northfield", "Harborview", "Summit Ridge", "Keystone", "Riverbend", "Granite Falls", "Bluewater",
        "Pinecrest", "Lakeshore", "Ironwood", "Cedar Valley", "Westbrook", "Clearwater", "Stonegate"
    ];

    private static readonly (string Industry, string Suffix)[] Industries =
    [
        ("utilities", "Regional Utilities"), ("healthcare", "Health System"), ("logistics", "Logistics"),
        ("finance", "Financial Group"), ("manufacturing", "Manufacturing"), ("government", "County Services"),
        ("energy", "Energy Cooperative"), ("education", "Community College")
    ];

    private static readonly string[] Cities =
    [
        "Springfield", "Riverton", "Fairview", "Franklin", "Greenville", "Madison", "Clinton", "Georgetown",
        "Salem", "Arlington", "Ashland", "Dover", "Milton", "Newport", "Oxford", "Burlington"
    ];

    private static readonly Dictionary<string, string[]> TitlesByDepartment = new()
    {
        ["Executive Office"] = ["Chief Executive Officer", "Chief Operating Officer", "Executive Assistant", "Chief of Staff"],
        ["Finance"] = ["Chief Financial Officer", "Controller", "Senior Accountant", "Financial Analyst", "Accounts Payable Specialist"],
        ["Human Resources"] = ["Director of Human Resources", "HR Business Partner", "Recruiter", "Benefits Coordinator"],
        ["Information Technology"] = ["Chief Information Officer", "IT Manager", "Network Engineer", "Systems Administrator", "Security Analyst", "Help Desk Technician"],
        ["Operations"] = ["Director of Operations", "Operations Manager", "Field Supervisor", "Operations Analyst", "Dispatcher"],
        ["Legal and Compliance"] = ["General Counsel", "Compliance Officer", "Paralegal"],
        ["Communications"] = ["Director of Communications", "Public Information Officer", "Communications Specialist"],
        ["Facilities"] = ["Facilities Manager", "Maintenance Technician", "Safety Coordinator"]
    };

    // repeatable titles for staff beyond each department's named positions
    private static readonly Dictionary<string, string[]> StaffTitles = new()
    {
        ["Executive Office"] = ["Executive Assistant"],
        ["Finance"] = ["Accounts Payable Specialist", "Financial Analyst"],
        ["Human Resources"] = ["HR Generalist"],
        ["Information Technology"] = ["Help Desk Technician", "Systems Administrator"],
        ["Operations"] = ["Field Technician", "Operations Analyst"],
        ["Legal and Compliance"] = ["Paralegal"],
        ["Communications"] = ["Communications Specialist"],
        ["Facilities"] = ["Maintenance Technician"]
    };

    private static readonly (string Name, string Host, string Os, string Role, string Department)[] SystemTemplates =
    [
        ("Domain Controller", "dc01", "Windows Server 2022", "Active Directory and DNS", "Information Technology"),
        ("Secondary Domain Controller", "dc02", "Windows Server 2022", "Active Directory and DNS", "Information Technology"),
        ("Mail Server", "mail01", "Windows Server 2019", "Exchange mail", "Information Technology"),
        ("File Server", "fs01", "Windows Server 2019", "Department file shares", "Information Technology"),
        ("ERP Application Server", "erp-app01", "Red Hat Enterprise Linux 9", "Finance and purchasing", "Finance"),
        ("ERP Database", "erp-db01", "Red Hat Enterprise Linux 9", "PostgreSQL for the ERP", "Finance"),
        ("Payroll Server", "payroll01", "Windows Server 2016", "Payroll and timekeeping", "Human Resources"),
        ("Intranet Web Server", "intranet01", "Ubuntu Server 22.04", "Internal portal and document library", "Communications"),
        ("VPN Gateway", "vpn01", "Appliance firmware 7.2", "Remote access", "Information Technology"),
        ("Perimeter Firewall", "fw01", "Appliance firmware 7.2", "Internet edge", "Information Technology"),
        ("Backup Server", "backup01", "Windows Server 2019", "Nightly backups", "Information Technology"),
        ("Operations Dispatch Server", "dispatch01", "Windows Server 2016", "Work orders and dispatch", "Operations"),
        ("Building Management Controller", "bms01", "Embedded Linux", "HVAC and badge access", "Facilities"),
        ("Help Desk Ticketing", "helpdesk01", "Ubuntu Server 22.04", "IT service tickets", "Information Technology")
    ];

    private static readonly string[] ProjectNames =
    [
        "Network Modernization", "ERP Upgrade", "Customer Portal Redesign", "Disaster Recovery Readiness",
        "Endpoint Refresh", "Records Digitization", "Fleet Telematics Rollout", "Identity and Access Cleanup",
        "Data Center Consolidation", "Workforce Scheduling Replacement"
    ];

    private static readonly string[] VendorNames =
    [
        "Brightline Networks", "Corvane Software", "Halberd Security", "Meridian Office Supply", "Tallgrass Consulting",
        "Vantage Telecom", "Northstar Facilities Group", "Pellucid Cloud Services", "Quarry Hill Hardware", "Ridgeway Insurance"
    ];

    private World(int seed, DateTime referenceDate, string organizationName, string domain)
    {
        Seed = seed;
        ReferenceDate = referenceDate.Date;

        var rng = new Random(seed);
        var prefix = Pick(rng, Prefixes);
        var (industry, suffix) = Pick(rng, Industries);
        Industry = industry;
        City = Pick(rng, Cities);
        OrganizationName = string.IsNullOrWhiteSpace(organizationName) ? $"{prefix} {suffix}" : organizationName.Trim();
        ShortName = OrganizationName.Split(' ')[0];
        // first and last word of the name, on a reserved top-level domain so no address is real
        var words = Slug(OrganizationName).Split('-');
        Domain = string.IsNullOrWhiteSpace(domain) ? $"{words[0]}{(words.Length > 1 ? words[^1] : "")}.example" : domain.Trim();
        Departments = TitlesByDepartment.Keys.ToList();

        People = CreatePeople(rng);
        Systems = CreateSystems(rng);
        Projects = Shuffle(rng, ProjectNames).Take(5).ToList();
        Vendors = Shuffle(rng, VendorNames).Take(6).ToList();
    }

    public static World Create(int seed, DateTime referenceDate, string organizationName = null, string domain = null) =>
        new(seed, referenceDate, organizationName, domain);

    /// <summary>
    /// Builds the world from configuration, filling an unset seed or reference date from the state
    /// file in <paramref name="contentRoot"/>, or creating that file when there is none.
    /// </summary>
    public static World Load(ApplicationConfiguration.WorldConfig config, string contentRoot, ILogger logger)
    {
        var statePath = Path.Combine(contentRoot, StateFileName);
        WorldState state = null;
        try
        {
            if (File.Exists(statePath))
                state = JsonSerializer.Deserialize<WorldState>(File.ReadAllText(statePath));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not read {StatePath}, starting a new world: {Message}", statePath, e.Message);
        }

        state ??= new WorldState(Random.Shared.Next(1, int.MaxValue), DateTime.UtcNow.Date);
        var seed = config.Seed ?? state.Seed;
        var referenceDate = config.ReferenceDate ?? state.ReferenceDate;

        try
        {
            Directory.CreateDirectory(contentRoot);
            File.WriteAllText(statePath, JsonSerializer.Serialize(new WorldState(state.Seed, state.ReferenceDate)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not write {StatePath}, the world will change on restart: {Message}", statePath, e.Message);
        }

        return new World(seed, referenceDate, config.OrganizationName, config.Domain);
    }

    public Person PersonIn(string department, Random rng)
    {
        var inDepartment = People.Where(p => p.Department == department).ToList();
        return inDepartment.Count > 0 ? inDepartment[rng.Next(inDepartment.Count)] : People[rng.Next(People.Count)];
    }

    /// <summary>The head of a department: its first person, who always holds the senior title.</summary>
    public Person HeadOf(string department) =>
        People.FirstOrDefault(p => p.Department == department) ?? People[0];

    private List<Person> CreatePeople(Random rng)
    {
        var people = new List<Person>();
        var usedNames = new HashSet<string>();
        var usedUsernames = new HashSet<string>();
        foreach (var department in Departments)
        {
            var titles = TitlesByDepartment[department];
            // the first title of each department is its head, then a few staff
            var count = titles.Length + rng.Next(1, 3);
            for (var i = 0; i < count; i++)
            {
                string first, last;
                do
                {
                    first = Pick(rng, FirstNames);
                    last = Pick(rng, LastNames);
                } while (!usedNames.Add($"{first} {last}"));

                var title = i < titles.Length ? titles[i] : Pick(rng, StaffTitles[department]);
                var username = $"{first[0]}{last}".ToLowerInvariant();
                for (var n = 2; !usedUsernames.Add(username); n++)
                    username = $"{first[0]}{last}{n}".ToLowerInvariant();
                people.Add(new Person(first, last, title, department,
                    $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@{Domain}",
                    $"555-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}", username));
            }
        }
        return people;
    }

    private List<SystemAsset> CreateSystems(Random rng)
    {
        var serverNet = rng.Next(10, 60);
        var hosts = new HashSet<int>();
        return SystemTemplates.Select(t =>
        {
            int host;
            do { host = rng.Next(10, 250); } while (!hosts.Add(host));
            return new SystemAsset(t.Name, $"{t.Host}.{Domain}", $"10.{serverNet}.10.{host}", t.Os, t.Role,
                PersonIn(t.Department, rng));
        }).ToList();
    }

    internal static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    internal static List<T> Shuffle<T>(Random rng, IEnumerable<T> items) => items.OrderBy(_ => rng.Next()).ToList();

    internal static string Slug(string value) =>
        new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Split('-', StringSplitOptions.RemoveEmptyEntries).Aggregate((a, b) => $"{a}-{b}");

    private record WorldState(int Seed, DateTime ReferenceDate);
}
