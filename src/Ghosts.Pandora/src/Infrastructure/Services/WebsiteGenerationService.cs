using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ghosts.Pandora.Infrastructure.Content;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IWebsiteGenerationService
{
    /// <summary>The HTML page for a website-mode path. Paths the site does not link to still get a page made from their words; null only for unusable paths.</summary>
    string RenderPage(string path);

    /// <summary>A not-found page in the site's own look, for the caller to send with a 404.</summary>
    string NotFoundPage();
}

/// <summary>
/// A whole website generated from the world's seed: every article or product has a stable id, and
/// every link on every page leads to a page that exists. Any other path gets a page made from its
/// words, so a visitor almost never sees a 404. Nothing is random per request, so the same URL
/// always shows the same page.
/// </summary>
public class WebsiteGenerationService(ApplicationConfiguration configuration, World world, ILogger<WebsiteGenerationService> logger)
    : IWebsiteGenerationService
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    // Lines are the story's own paragraphs: "## " starts a subhead. Image is a /img path, or "match" or "team" for a team graphic.
    private record Item(int Id, string Section, string Kind, string Headline, string Summary, Writer Author, DateTime Published,
        string Image, Facts Facts, string[] Lines, Offer Offer);

    private record Writer(string Name, string Beat, string Photo)
    {
        public string Slug => World.Slug(Name);
        public string Initials => string.Concat(Name.Split(' ').Select(w => w[0]));
    }

    private record Team(string City, string Full, string Short, string Abbr, string Primary, string Secondary, bool Club);

    private record Facts(string Sport, Team Team, Team Other, int Score, int OtherScore, string Title, string Album, string Artist,
        string Network, string Studio, string Star, string Star2, Person Person, string Local, string Local2, int Number, string Day, int Variant = 0);

    private record Story(string Kind, string Theme, string Headline, string Dek, string[] Lines);

    private record ProductType(string Department, string[] Names, (string Color, string Image)[] Variants, string Extra, int Low, int High,
        string Blurb, string[] Features, (string Name, string Value)[] Specs, string[] Sizes, (int Stars, string Title, string Text)[] Reviews);

    private record Offer(ProductType Type, string Color, string Image, decimal Price, decimal ListPrice, double Rating, int Ratings, string Badge);

    private record Game(string Sport, Team Winner, Team Loser, int WinnerScore, int LoserScore, string Status, Item Recap);

    private record Site(string Type, string Name, string Accent, string Tagline, string[] Sections, List<Item> Items, List<Writer> Writers, List<Game> Games);

    private Site _site;

    private string SiteType => (configuration.Mode.SiteType ?? "news").ToLowerInvariant() switch
    {
        "shopping" or "ecommerce" => "shopping",
        "sports" => "sports",
        "entertainment" => "entertainment",
        _ => "news"
    };

    public string RenderPage(string path)
    {
        var site = _site ??= CreateSite();
        var segments = (path ?? "/").Trim('/').ToLowerInvariant().Split('/', StringSplitOptions.RemoveEmptyEntries);
        logger.LogInformation("Rendering {SiteType} page {Path}", site.Type, path);

        if (segments.Length > 8 || segments.Any(s => s.Length > 100))
            return null;

        if (segments.Length == 0)
            return Home(site);

        var itemRoute = site.Type == "shopping" ? "product" : "article";
        if (segments.Length == 2 && segments[0] == itemRoute && int.TryParse(segments[1], out var id))
        {
            var item = site.Items.FirstOrDefault(i => i.Id == id);
            if (item != null)
                return site.Type == "shopping" ? Product(site, item) : Article(site, item);
        }

        if (segments.Length == 2 && segments[0] == "author" && site.Type != "shopping")
        {
            var writer = site.Writers.FirstOrDefault(w => w.Slug == segments[1]);
            if (writer != null)
                return AuthorPage(site, writer);
        }

        if (segments.Length == 1)
        {
            var section = site.Sections.FirstOrDefault(s => Slug(s) == segments[0]);
            if (section != null)
                return SectionPage(site, section);

            if (segments[0] is "about" or "contact" or "privacy" or "terms")
                return InfoPage(site, segments[0]);

            if (site.Type == "shopping" && segments[0] == "cart")
                return Cart(site);

            // the header search form submits here; the page filters its own products by the query
            if (site.Type == "shopping" && segments[0] == "s")
                return SearchPage(site);
        }

        return PathPage(site, segments);
    }

    public string NotFoundPage() => Page(_site ??= CreateSite(), "Page not found",
        "<div class=\"info\"><h1>Page not found</h1><p>Sorry, the page you are looking for has moved or no longer exists.</p><p><a class=\"info-link\" href=\"/\">Go to the home page</a></p></div>");

    #region Site content

    private static readonly string[] NewsSections = ["Local", "Nation", "World", "Politics", "Business", "Technology", "Science", "Health", "Sports", "Arts", "Opinion"];
    private static readonly string[] SportsSections = ["Football", "Basketball", "Baseball", "Soccer", "Hockey", "College", "Golf", "Tennis"];
    private static readonly string[] EntertainmentSections = ["Film", "TV", "Music", "Celebrity", "Streaming", "Box Office", "Reviews", "Awards"];
    private static readonly string[] ShoppingSections = ["Women", "Men", "Kids", "Kitchen & Dining", "Office & School", "Bags & Totes", "Outdoor"];

    private static readonly string[] TeamCities = ["Riverton", "Fairview", "Madison", "Salem", "Dover", "Ashland", "Newport", "Clinton", "Oxford", "Milton"];

    private static readonly Dictionary<string, string[]> Nicknames = new()
    {
        ["Football"] = ["Rapids", "Falcons", "Miners", "Sentinels", "Dockers", "Aces", "Navigators", "Comets", "Owls", "Mustangs"],
        ["Basketball"] = ["Current", "Flyers", "Monarchs", "Spirits", "Drive", "Hawks", "Tide", "Cyclones", "Scholars", "Blaze"],
        ["Baseball"] = ["River Dogs", "Foxes", "Mudcats", "Sluggers", "Clippers", "Aviators", "Pilots", "Cardinals", "Oaks", "Millers"],
        ["Soccer"] = ["FC", "United", "City", "SC", "Athletic", "Rovers", "County", "Albion", "Wanderers", "Town"],
        ["Hockey"] = ["Ice Hawks", "Freeze", "Blades", "Huskies", "Wolves", "Lumberjacks", "Mariners", "Chill", "Lynx", "Moose"],
        ["College"] = ["State Bobcats", "Tech Engineers", "College Bears", "University Lions", "State Gulls", "A&M Rams", "College Seahawks", "State Wildcats", "University Dons", "Valley Badgers"]
    };

    private static readonly (string Primary, string Secondary)[] Palette =
    [
        ("#002244", "#fb4f14"), ("#7a0019", "#ffcc33"), ("#154734", "#ffffff"), ("#0033a0", "#ffffff"), ("#111111", "#d50a0a"), ("#00778b", "#111111"),
        ("#4b2e83", "#e8d3a2"), ("#c8102e", "#041e42"), ("#00843d", "#ffd100"), ("#e04e1b", "#111111"), ("#2f6fb5", "#041e42"), ("#8a1538", "#a2aaad")
    ];

    // declared after the arrays above, which static initialization reads in order
    private static readonly Dictionary<string, List<Team>> Leagues = Nicknames.Keys.Select((sport, k) => (sport, k)).ToDictionary(x => x.sport, x =>
        TeamCities.Select((city, i) =>
        {
            var nick = Nicknames[x.sport][i];
            var (primary, secondary) = Palette[(i * 5 + x.k * 3) % Palette.Length];
            var shortName = x.sport == "Soccer" ? city : x.sport == "College" ? nick.Split(' ')[^1] : nick;
            return new Team(city, $"{city} {nick}", shortName, city[..3].ToUpperInvariant(), primary, secondary, x.sport == "Soccer");
        }).ToList());

    private static readonly string[] Shows = ["The Long Harbor", "Night Shift", "Glass Houses", "Northern Lights", "The Quiet Year", "Second Chances", "Open Water", "City of Bridges", "The Last Lighthouse", "Small Town Summer"];
    private static readonly string[] Films = ["Salt and Ember", "The Weight of Rain", "Midnight at Carrow Bay", "Paper Kingdoms", "Low Tide", "A Brief Light", "Iron Orchard", "The Cartographer"];
    private static readonly string[] Albums = ["Glass Hours", "Neon Hymnal", "Slow Motion Summer", "Static and Gold", "Maps of the Moon", "Wildflower Radio", "Low Light", "Northbound"];
    private static readonly string[] Artists = ["The Paper Lanterns", "Juno Reyes", "Velvet Static", "Marlowe Vance", "The Northern Echo", "Sable Kingsley", "Harbor Lights", "Coco Vale"];
    private static readonly string[] Networks = ["Brightbox", "Lantern+", "Northstar", "Streamline", "Channel 9"];
    private static readonly string[] Studios = ["Lantern Pictures", "Meridian Films", "Copperleaf Studios", "Bluegate Pictures"];

    private static readonly string[] StarWomen = ["Ava", "Zoe", "Ivy", "Nora", "Mila", "Sienna", "Freya", "Lena", "Margot", "Cleo", "Wren"];
    private static readonly string[] StarMen = ["Liam", "Mason", "Theo", "Jude", "Rhys", "Caleb", "Dante", "Felix", "August", "Silas"];
    private static readonly string[] StarFirst = [.. StarWomen, .. StarMen];
    private static readonly string[] AthleteFirst = ["Marcus", "Tyler", "Devin", "Jalen", "Cole", "Darius", "Trey", "Bryce", "Andre", "Luis", "Miguel", "Kyle", "Isaiah", "Malik", "Nolan", "Garrett", "Xavier", "Brandon", "Mateo", "Connor"];

    // portraits by who is in them, so a caption never names a man under a photo of a woman
    private static readonly HashSet<string> WomenPortraits = ["post-photo6.jpg", "post__thumb7.jpg", "team2.jpg", "team5.jpg", "open-photo1.jpg", "post13.jpg", "post-header1.jpg", "video7.jpg"];
    private static readonly HashSet<string> MenPortraits = ["last-phot15-large.jpg", "post5.jpg", "team6.jpg", "team3.jpg"];
    private static readonly string[] StarLast = ["Hartwell", "Castellane", "Sinclair", "Vale", "Ashby", "Kingsley", "Delacroix", "Lindell", "Marchetti", "Prescott", "Quill", "Saroyan", "Thorne", "Whitlock", "Yardley", "Calloway", "Ferrante", "Okonkwo", "Brandt", "Sorel"];

    // invented locals, kept apart from the world's people so a quote never lands on an employee by accident
    private static readonly string[] LocalFirst = ["Pat", "Lee", "Sam", "Robin", "Chris", "Terry", "Leslie", "Frances", "Glenn", "Rosa", "Walt", "Imani", "Theo", "Carmen", "Desmond", "Yolanda", "Arturo", "Bridget", "Colin", "Delia", "Ezra", "Hattie", "Ivan", "Joy", "Kurt", "Lena", "Nell", "Otis", "Paloma", "Reggie", "Selma", "Tobias", "Vince", "Wanda", "Marcus", "Gloria"];
    private static readonly string[] LocalLast = ["Ellison", "Vaughn", "Mercer", "Cho", "Barlow", "Kowalski", "Ostrander", "Pruitt", "Quinlan", "Rasmussen", "Sandoval", "Talbot", "Underwood", "Vasquez", "Whitaker", "Yates", "Zimmer", "Abernathy", "Beaumont", "Castellano", "Dunleavy", "Eckert", "Fairbanks", "Gallagher", "Haddad", "Iverson", "Jaramillo", "Kearney", "Lockhart", "Mahoney", "Nakamura", "Oyelaran", "Petrakis", "Rourke", "Szabo", "Thornton"];

    private static readonly Dictionary<string, string[]> Photos = new()
    {
        ["city"] = ["post__thumb3.jpg", "top-header2.jpg", "post4.jpg", "video14.jpg", "photo-album3.jpg", "photo-item1.jpg", "last-photo2-large.jpg"],
        ["weather"] = ["post__thumb6.jpg", "post__thumb3.jpg", "video16.jpg"],
        ["outdoors"] = ["last-phot16-large.jpg", "last-phot12-large.jpg", "post__thumb4.jpg", "photo-item6.jpg", "photo-item9.jpg", "post-photo2.jpg", "top-header1.jpg", "video16.jpg", "post8.jpg"],
        ["wildlife"] = ["photo-item8.jpg", "post-video.jpg", "photo-item9.jpg", "post-photo2.jpg"],
        ["community"] = ["video1.jpg", "video9.jpg", "video14.jpg", "photo-album3.jpg", "post__thumb5.jpg"],
        ["office"] = ["post3.jpg", "photo-item11.jpg", "photo-item10.jpg", "post-thumb1.jpg"],
        ["tech"] = ["post-header.jpg", "post3.jpg", "post-thumb1.jpg", "photo-item10.jpg", "photo-item11.jpg"],
        ["health"] = ["post9.jpg", "post8.jpg", "last-phot16-large.jpg", "photo-item9.jpg", "post-photo4.jpg"],
        ["travel"] = ["last-phot12-large.jpg", "post-photo4.jpg", "photo-item6.jpg", "post__thumb3.jpg"],
        ["portrait"] = ["post-photo6.jpg", "post__thumb7.jpg", "team2.jpg", "team5.jpg", "open-photo1.jpg", "post13.jpg", "post-header1.jpg", "last-phot15-large.jpg", "post5.jpg", "team6.jpg", "team3.jpg", "video7.jpg"],
        ["stage"] = ["video4.jpg", "photo-album10.jpg", "video2.jpg", "video8.jpg", "last-photo4-large.jpg", "video9.jpg"],
        ["studio"] = ["last-photo7-large.jpg", "video6.jpg", "photo-album8.jpg", "photo-album5.jpg", "video12.jpg", "slide1.jpg"],
        ["records"] = ["last-photo3-large.jpg", "last-photo8-large.jpg", "last-photo9-large.jpg", "video5.jpg"],
        ["nightlife"] = ["last-photo4-large.jpg", "last-photo2-large.jpg", "top-header2.jpg", "post4.jpg"],
        ["streaming"] = ["post-thumb1.jpg", "post-header.jpg", "post4.jpg", "top-header2.jpg"],
        ["art"] = ["post1.jpg", "last-photo9-large.jpg", "photo-item1.jpg", "photo-album3.jpg", "post__thumb1.jpg"],
        ["fans"] = ["video9.jpg", "video4.jpg", "photo-album10.jpg", "video2.jpg", "video1.jpg"],
        ["athlete"] = ["team1.jpg", "team3.jpg", "post5.jpg", "last-phot15-large.jpg"],
        ["course"] = ["post__thumb4.jpg", "post-photo2.jpg", "top-header1.jpg", "photo-item6.jpg"]
    };

    // photos big enough to lead a page
    private static readonly HashSet<string> LargePhotos =
    [
        "/img/post-header.jpg", "/img/post-header1.jpg", "/img/post-thumb1.jpg", "/img/video9.jpg", "/img/post__thumb3.jpg", "/img/post__thumb4.jpg",
        "/img/post__thumb5.jpg", "/img/post__thumb6.jpg", "/img/post__thumb7.jpg", "/img/post-video.jpg", "/img/video4.jpg", "/img/video6.jpg",
        "/img/post-photo2.jpg", "/img/post-photo6.jpg", "/img/top-header1.jpg", "/img/top-header2.jpg", "/img/open-photo1.jpg",
        "/img/last-phot12-large.jpg", "/img/last-phot15-large.jpg", "/img/last-phot16-large.jpg", "/img/last-photo2-large.jpg", "/img/last-photo3-large.jpg",
        "/img/last-photo4-large.jpg", "/img/last-photo7-large.jpg", "/img/last-photo8-large.jpg", "/img/last-photo9-large.jpg"
    ];

    private Site CreateSite()
    {
        var rng = new Random(world.Seed ^ 0x5173);
        var type = SiteType;
        var name = string.IsNullOrWhiteSpace(configuration.Mode.SiteName) ? $"{world.City} Daily" : configuration.Mode.SiteName;
        var (accent, tagline, sections) = type switch
        {
            "sports" => ("#d50a0a", "Live Scores, Highlights and Sports News", SportsSections),
            "entertainment" => ("#e6007e", "Celebrity News, Movies, TV and Music", EntertainmentSections),
            "shopping" => ("#f0592a", "Everyday Basics, Delivered Fast", ShoppingSections),
            _ => ("#b4232a", "Breaking News, Local News and Analysis", NewsSections)
        };

        var count = Math.Clamp(configuration.Mode.ArticleCount, 4, 100) * 3;
        var ids = new HashSet<int>();
        int NextId()
        {
            int id;
            do { id = rng.Next(10000, 99999); } while (!ids.Add(id));
            return id;
        }

        var writers = type == "shopping" ? new List<Writer>() : CreateWriters(sections, rng);
        var items = (type == "shopping" ? CreateProducts(name, count, NextId, rng) : CreateStories(type, count, sections, writers, NextId, rng))
            .OrderByDescending(i => i.Published).ToList();
        return new Site(type, name, accent, tagline, sections, items, writers, type == "sports" ? CreateGames(items, rng) : new List<Game>());
    }

    private static List<Writer> CreateWriters(string[] sections, Random rng)
    {
        string[] first = ["Alex", "Jordan", "Casey", "Morgan", "Taylor", "Jamie", "Riley", "Avery", "Quinn", "Dana", "Elliot", "Harper", "Rowan", "Sasha", "Drew", "Blake"];
        string[] last = ["Holloway", "Brennan", "Okoro", "Lindqvist", "Marsh", "Delgado", "Whitfield", "Tanaka", "Ferris", "Abbott", "Castillo", "Pemberton", "Ngata", "Sorensen", "Achebe", "Moreau"];
        var headshots = new Queue<string>(["team4.jpg", "team1.jpg", "photo-item7.jpg"]);
        var writers = new List<Writer>();
        foreach (var section in sections)
        {
            // a newsroom has a few columnists; every other beat gets one reporter
            for (var n = section == "Opinion" ? 3 : 1; n > 0; n--)
            {
                string name;
                do name = $"{Pick(rng, first)} {Pick(rng, last)}"; while (writers.Any(w => w.Name == name));
                writers.Add(new Writer(name, section, section == "Opinion" ? "/img/" + headshots.Dequeue() : null));
            }
        }
        return writers;
    }

    private List<Item> CreateStories(string type, int count, string[] sections, List<Writer> writers, Func<int> nextId, Random rng)
    {
        var items = new List<Item>();
        var used = new Dictionary<string, int>();
        var headlines = new HashSet<string>();
        var offset = rng.Next(100);
        for (var i = 0; i < count; i++)
        {
            var section = sections[i % sections.Length];
            var published = world.ReferenceDate.AddHours(12).AddMinutes(-rng.Next(12, 60 * 24 * 9));
            var facts = NewFacts(type, section, published, rng);
            var templates = type switch { "sports" => SportsStories(section), "entertainment" => EntertainmentStories(section), _ => NewsStories(section) };
            var sectionIndex = i % sections.Length;
            var nth = i / sections.Length;
            // each story in a section is about a different show, film and album
            if (type == "entertainment")
                facts = facts with
                {
                    Title = (section is "TV" or "Streaming" ? Shows : Films)[(sectionIndex * 3 + nth) % (section is "TV" or "Streaming" ? Shows.Length : Films.Length)],
                    Album = Albums[(sectionIndex + nth) % Albums.Length], Artist = Artists[(sectionIndex * 2 + nth) % Artists.Length]
                };
            // walk the section's templates in order so headlines do not repeat until they run out, and
            // start each section somewhere else so neighboring sections lead with different stories;
            // a headline already on the site moves on to the section's next template
            var slot = (offset + sectionIndex * 3) % templates.Length + nth;
            Func<Facts, Story> template;
            Story story;
            var tries = 0;
            do
            {
                template = templates[slot % templates.Length];
                facts = facts with { Variant = slot / templates.Length + sectionIndex };
                story = template(facts);
                slot++;
            } while (!headlines.Add(story.Headline) && ++tries < templates.Length);
            var image = story.Theme is "match" or "team" ? story.Theme : PickPhoto(story.Theme, used, rng);
            if (type == "entertainment" && FitStarToPhoto(facts, image) is var fitted && fitted != facts)
            {
                facts = fitted;
                story = template(facts);
            }
            var beat = writers.Where(w => w.Beat == section).ToList();
            items.Add(new Item(nextId(), section, story.Kind, story.Headline, story.Dek, beat[rng.Next(beat.Count)], published, image, facts, story.Lines, null));
        }
        return items;
    }

    // the star's first name swapped for one that matches the person in a portrait
    private static Facts FitStarToPhoto(Facts facts, string image)
    {
        var file = image.StartsWith('/') ? image[5..] : "";
        var first = facts.Star.Split(' ')[0];
        var names = WomenPortraits.Contains(file) && !StarWomen.Contains(first) ? StarWomen
            : MenPortraits.Contains(file) && !StarMen.Contains(first) ? StarMen : null;
        return names == null ? facts : facts with { Star = $"{names[facts.Number % names.Length]} {L(facts.Star)}" };
    }

    // the least used photo of the theme, so a page repeats a photo only when its theme runs out
    private static string PickPhoto(string theme, Dictionary<string, int> used, Random rng)
    {
        var pool = Photos[theme];
        var least = pool.Min(p => used.GetValueOrDefault(p));
        var choices = pool.Where(p => used.GetValueOrDefault(p) == least).ToList();
        var photo = choices[rng.Next(choices.Count)];
        used[photo] = least + 1;
        return "/img/" + photo;
    }

    private Facts NewFacts(string type, string section, DateTime published, Random rng)
    {
        var sport = type == "sports" && Leagues.ContainsKey(section) ? section : "Football";
        var league = Leagues[sport];
        var team = league[rng.Next(league.Count)];
        var others = league.Where(t => t != team).ToList();
        var (score, otherScore) = Scores(sport, rng);
        var (first, last) = type == "entertainment" ? (StarFirst, StarLast) : type == "sports" || section == "Sports" ? (AthleteFirst, LocalLast) : (LocalFirst, LocalLast);
        var star = $"{Pick(rng, first)} {Pick(rng, last)}";
        string star2;
        do star2 = $"{Pick(rng, first)} {Pick(rng, last)}"; while (star2.Split(' ')[^1] == star.Split(' ')[^1]);
        var local = $"{Pick(rng, LocalFirst)} {Pick(rng, LocalLast)}";
        string local2;
        do local2 = $"{Pick(rng, LocalFirst)} {Pick(rng, LocalLast)}"; while (local2.Split(' ')[^1] == local.Split(' ')[^1] || local2.Split(' ')[^1] == star.Split(' ')[^1]);
        // a department head other than the chief executive, for appointments
        var person = world.HeadOf(Pick(rng, world.Departments.Where(d => d != "Executive Office").ToList()));
        return new Facts(sport, team, others[rng.Next(others.Count)], score, otherScore,
            section is "TV" or "Streaming" ? Pick(rng, Shows) : Pick(rng, Films), Pick(rng, Albums), Pick(rng, Artists), Pick(rng, Networks), Pick(rng, Studios),
            star, star2, person, local, local2, rng.Next(2, 96), published.ToString("dddd", Us));
    }

    private static (int, int) Scores(string sport, Random rng)
    {
        var winner = sport switch { "Football" or "College" => rng.Next(17, 42), "Basketball" => rng.Next(96, 131), "Baseball" => rng.Next(3, 11), "Soccer" => rng.Next(1, 4), _ => rng.Next(2, 6) };
        var loser = sport switch { "Football" or "College" => winner - rng.Next(1, 15), "Basketball" => winner - rng.Next(1, 16), _ => rng.Next(0, winner) };
        return (winner, loser);
    }

    private static List<Game> CreateGames(List<Item> items, Random rng)
    {
        var games = items.Where(i => i.Kind == "recap").Take(12)
            .Select(i => new Game(i.Facts.Sport, i.Facts.Team, i.Facts.Other, i.Facts.Score, i.Facts.OtherScore, "Final", i)).ToList();
        // the rest of the night's slate: finished games without a story, then tonight's games
        foreach (var sport in Leagues.Keys)
        {
            var league = Leagues[sport];
            for (var n = 0; n < 3; n++)
            {
                var a = league[rng.Next(league.Count)];
                var b = league.Where(t => t != a).ElementAt(rng.Next(league.Count - 1));
                var (w, l) = Scores(sport, rng);
                games.Add(n < 2 ? new Game(sport, a, b, w, l, "Final", null) : new Game(sport, a, b, -1, -1, $"{rng.Next(6, 10)}:{Pick(rng, ["00", "05", "30", "35"])} PM", null));
            }
        }
        return games.OrderBy(g => g.Status != "Final").ThenBy(g => Array.IndexOf(SportsSections, g.Sport)).ToList();
    }

    private Person Ceo => world.HeadOf("Executive Office");
    private Person Cio => world.HeadOf("Information Technology");
    private Person Cfo => world.HeadOf("Finance");
    private Person Spokesperson => world.HeadOf("Communications");

    private string OrgNoun => world.Industry switch
    {
        "utilities" => "utility", "healthcare" => "health system", "government" => "county", "energy" => "cooperative", "education" => "college", _ => "company"
    };

    private string Customers => world.Industry switch
    {
        "healthcare" => "patients", "logistics" => "shippers", "finance" => "account holders", "government" => "residents", "education" => "students", _ => "customers"
    };

    private string Service => world.Industry switch
    {
        "utilities" => "its online billing portal", "healthcare" => "its patient portal and appointment line", "logistics" => "shipment tracking",
        "finance" => "online and mobile banking", "manufacturing" => "order processing", "government" => "online permits and records requests",
        "energy" => "its outage map and billing system", "education" => "student registration and online classes", _ => "its online services"
    };

    private string ServiceShort => world.Industry switch
    {
        "utilities" => "billing portal", "healthcare" => "patient portal", "logistics" => "shipment tracking", "finance" => "online banking",
        "manufacturing" => "order systems", "government" => "online permits", "energy" => "outage map", "education" => "student portal", _ => "online services"
    };

    private int Employees => 400 + world.Seed % 1600;
    private string Phone => $"555-{world.Seed % 900 + 100}-0100";

    private string Of(Person p) => $"{p.FullName}, the {OrgNoun}'s {p.Title.ToLowerInvariant()}";

    private Func<Facts, Story>[] NewsStories(string section)
    {
        var city = world.City;
        var at = city.ToUpperInvariant() + " — ";
        var org = world.OrganizationName;
        var brand = world.ShortName;
        var noun = OrgNoun;
        var customers = Customers;
        var (ceo, cio, cfo, sp) = (Ceo, Cio, Cfo, Spokesperson);
        return section switch
        {
            "Local" =>
            [
                f => new Story("civic", "weather", $"Crews work through the night as storm cleanup continues across {city}",
                    "Downed limbs and scattered power outages kept public works crews busy into the morning, and officials asked drivers to avoid flooded side streets.",
                [
                    $"{at}Public works crews spent a second straight night clearing fallen trees and clogged storm drains across {city} after a line of thunderstorms dropped close to three inches of rain on parts of the city in a few hours.",
                    $"At the height of the storm, about {f.Number * 140 + 900:N0} homes and businesses were without power, according to utility estimates. Most had service back by early afternoon, though residents on a handful of north side streets were told to expect longer waits.",
                    $"\"We had every truck we own on the road by midnight,\" said {f.Local}, the city's public works director. \"Our crews will keep at it until the last drain on the list is clear.\"",
                    "## Schools open late, recycling delayed",
                    "Several intersections near the river stayed closed for most of the morning while crews pumped water from low-lying streets. Two elementary schools opened two hours late, and curbside recycling was pushed back a day.",
                    $"On Elm Street, {f.Local2} spent the morning dragging branches to the curb. \"The water came up faster than I've ever seen it,\" {L(f.Local2)} said. \"Twenty minutes, and the whole block was a creek.\"",
                    $"The city is collecting storm debris left at the curb through next week. Residents can report blocked drains or downed lines by calling 311 or using the city's mobile app."
                ]),
                f => new Story("civic", "community", $"Thousands turn out as {city}'s street festival returns downtown",
                    "Organizers said attendance topped last year's mark, helped by mild weather and a longer list of local vendors.",
                [
                    $"{at}Food trucks, a three-block craft market and a steady stream of live music drew thousands of people downtown over the weekend for {city}'s annual street festival, which organizers said was the best attended in its 26-year history.",
                    $"More than {f.Number + 90} vendors set up along Main and Second streets, up from about 70 last year. Organizers estimated weekend attendance at roughly {f.Number * 300 + 18000:N0}.",
                    $"\"We were nervous about the forecast all week, and then it turned out perfect,\" said {f.Local}, who chairs the festival committee. \"Seeing families on every corner, that's the whole point.\"",
                    "## A boost for downtown businesses",
                    "Several restaurants along the route said they had their busiest weekend of the year. The city closed both streets to traffic from Friday evening through Sunday night and added extra trash and recycling pickups.",
                    $"\"I sold out by Saturday afternoon and had to call home for more,\" said {f.Local2}, who runs a ceramics stall. \"I'll be back next year with twice as much.\"",
                    "Organizers said planning for next year's festival begins in January, and they are looking for volunteers."
                ]),
                f => new Story("civic", "city", "Main Street bridge reopens weeks ahead of schedule",
                    "The span had been closed since spring for deck and railing repairs. Commuters said the detour added as much as 20 minutes each way.",
                [
                    $"{at}The Main Street bridge reopened to traffic early {f.Day} morning, about five weeks ahead of schedule, ending a detour that had frustrated commuters and downtown businesses for most of the year.",
                    $"The ${f.Number % 9 + 4}.{f.Number % 10} million project replaced the bridge deck, repaired its steel supports and added wider sidewalks and a protected bike lane. The state paid for about two-thirds of the cost.",
                    $"\"The contractor caught a stretch of good weather and made the most of it,\" said {f.Local}, the city engineer. \"We'll be back for some finishing work at night, but the lanes are open.\"",
                    "## Businesses hope customers return",
                    "Business owners on both sides of the river said the closure had cut into foot traffic. Several said they hoped the reopening would bring customers back in time for the busiest months of the year.",
                    $"\"It's like someone turned the lights back on,\" said {f.Local2}, who owns a coffee shop a block from the bridge. \"People are already walking in saying they missed us.\"",
                    "Overnight lane closures for paint and lighting work will continue for about three weeks, the city said."
                ]),
                f => new Story("org", "city", $"{org} plans new operations center on {city}'s west side",
                    $"The project would bring about {f.Number + 60} jobs and consolidate work now spread across three leased buildings.",
                [
                    $"{at}{org} plans to build a new operations center on {city}'s west side, a project the {noun} says would bring about {f.Number + 60} new jobs and consolidate work now spread across three leased buildings.",
                    $"The two-story building would sit on a 12-acre parcel near the highway interchange. {brand} expects to seek site plan approval from the city's planning commission early next year, with construction to follow.",
                    $"\"We have outgrown the space we have, and we want to stay in {city},\" said {Of(ceo)}. \"This keeps our people close to the {customers} we serve.\"",
                    "## Neighbors ask about traffic",
                    $"Council member {f.Local}, whose ward includes the site, said residents had already asked about traffic on the two-lane road that leads to the parcel. \"I'm glad they're staying, but we need a real plan for that road before the first shovel goes in,\" {L(f.Local)} said.",
                    $"{sp.FullName}, the {noun}'s {sp.Title.ToLowerInvariant()}, said {brand} would pay for a traffic study and hold an open house for neighbors before the planning commission meets."
                ])
            ],
            "Nation" =>
            [
                f => new Story("nation", "office", $"Employers add {f.Number + 110},000 jobs as hiring holds steady",
                    "Wage growth cooled slightly and the unemployment rate was unchanged, a sign the labor market is settling into a slower pace.",
                [
                    $"WASHINGTON — U.S. employers added {f.Number + 110},000 jobs last month, the Labor Department said {f.Day}, a steady if unspectacular gain that suggests the job market is cooling gradually rather than cracking.",
                    $"The unemployment rate held at 4.{f.Number % 3 + 1} percent. Average hourly earnings rose 0.3 percent from the previous month and 3.{f.Number % 6 + 3} percent from a year earlier.",
                    $"\"This is what a soft landing looks like on paper,\" said {f.Local}, chief economist at Harrow & Lane Advisors. \"Companies aren't laying people off, but they're being much choosier about adding them.\"",
                    "## Where the jobs were",
                    "Health care, government and construction accounted for most of the gains. Retail and manufacturing employment was little changed, and temporary help agencies shed jobs for a fifth straight month.",
                    $"{f.Local2}, an economist at the Midland Policy Institute, said the figures would likely keep the Federal Reserve on hold at its next meeting. \"Nothing in this report forces anyone's hand,\" {L(f.Local2)} said.",
                    "The next jobs report is due in about four weeks."
                ]),
                f => new Story("nation", "weather", "Early-season storm snarls travel across the northern Plains",
                    "Blowing snow closed stretches of two interstates, and airlines canceled hundreds of flights at hubs in the upper Midwest.",
                [
                    $"BISMARCK, N.D. — A fast-moving storm brought heavy snow and gusts above 50 miles per hour to the northern Plains on {f.Day}, closing long stretches of two interstates and stranding drivers at truck stops across three states.",
                    $"Airlines canceled more than {f.Number * 9 + 200} flights at hubs in the upper Midwest, according to the tracking site FlightGrid, and dozens of school districts called off classes.",
                    $"\"It's not the amount of snow, it's the wind,\" said {f.Local}, a meteorologist with the National Weather Service. \"Visibility on open highway is close to zero in places.\"",
                    "## Crews work to reopen roads",
                    "Road crews worked to reopen the highways by evening, but officials urged drivers to stay home until the wind eased. Utilities reported scattered outages in rural areas.",
                    $"{f.Local2}, a trucker headed east with a load of grain, spent the night in a truck stop parking lot. \"You don't argue with a whiteout,\" {L(f.Local2)} said. \"You wait.\""
                ]),
                f => new Story("nation", "city", "Housing starts rise for a third straight month",
                    "Builders broke ground on more single-family homes, though permits for new apartment buildings fell.",
                [
                    $"WASHINGTON — Construction began on more new homes last month for the third month in a row, the Commerce Department said {f.Day}, as builders bet that easing mortgage rates will bring buyers back.",
                    $"Housing starts rose {f.Number % 7 + 2}.{f.Number % 10} percent to a seasonally adjusted annual rate of about 1.4 million units. Single-family starts led the increase, while starts on buildings with five or more units declined.",
                    $"\"Builders are finally seeing a little daylight,\" said {f.Local}, a housing analyst at Stonebridge Research. \"Buyers who were waiting for rates to come down are starting to come back.\"",
                    "## Costs remain a hurdle",
                    "Permits, a gauge of future construction, were flat. Builders continue to cite high land and labor costs, and many say they are offering incentives such as mortgage rate buydowns to close sales.",
                    $"\"The math still doesn't work for a lot of first-time buyers,\" said {f.Local2}, who runs a homebuilding company outside Denver."
                ]),
                f => new Story("nation", "city", $"Federal grants to fund electric buses in 40 cities, including {city}",
                    $"{city} will receive money for 12 new buses and charging equipment at its downtown garage.",
                [
                    $"WASHINGTON — The Transportation Department on {f.Day} announced grants to replace aging diesel buses with electric and hybrid models in 40 cities, including {city}, which will receive enough money for 12 new buses and charging equipment.",
                    "The program, funded through a multiyear infrastructure law, is meant to cut fuel and maintenance costs for transit agencies as well as emissions. Cities must cover at least 15 percent of the cost.",
                    $"\"Our oldest buses are 16 years old, and they spend more time in the shop than on the road,\" said {f.Local}, general manager of {city}'s transit authority. \"This lets us retire the worst of them.\"",
                    "## Questions about winter range",
                    "Critics in Congress have questioned whether electric buses perform well in cold climates, and some agencies have reported range problems in winter. Supporters say newer models have largely solved those issues.",
                    $"The first of {city}'s new buses are expected on the road in about 18 months, transit officials said."
                ])
            ],
            "World" =>
            [
                f => new Story("world", "travel", "Shipping rates ease as congestion clears at major Asian ports",
                    "Freight costs on key routes fell for a fourth week, offering relief to retailers stocking up for the busy season.",
                [
                    "SINGAPORE — The cost of shipping a container from Asia to Europe and North America fell for a fourth straight week, as congestion that had backed up vessels at several major ports eased and carriers added capacity.",
                    $"The benchmark rate for a 40-foot container from Shanghai to Rotterdam fell about {f.Number % 12 + 4} percent from the previous week, according to a freight index compiled by Baltic Line Analytics.",
                    $"\"The bottleneck has broken, at least for now,\" said {f.Local}, a shipping analyst in Singapore. \"Ships that were waiting a week to berth are getting in within a day or two.\"",
                    "## Relief may take months to reach stores",
                    "Retailers had warned that high freight costs could push up prices on electronics, furniture and clothing. Analysts said lower rates could take several months to show up on store shelves.",
                    $"\"Contracts signed at the peak are still working their way through,\" {L(f.Local)} said."
                ]),
                f => new Story("world", "outdoors", "Climate talks open with new pledges on methane and forests",
                    "Delegates from nearly 190 countries are meeting for two weeks, with financing for poorer nations expected to dominate.",
                [
                    $"GENEVA — International climate talks opened {f.Day} with a fresh set of pledges to cut methane emissions and protect forests, as negotiators from nearly 190 countries began two weeks of meetings expected to center on money for poorer nations.",
                    "More than 30 countries signed on to a new goal of reducing methane from oil and gas operations by 40 percent by the end of the decade. A separate group announced a fund to pay landowners in tropical countries to keep forests standing.",
                    $"\"Pledges are the easy part,\" said {f.Local}, who directs climate policy research at the Halvorsen Institute. \"The question is whether anyone shows up with real money.\"",
                    "## The fight over financing",
                    "Developing countries have pressed wealthier nations to triple the financing they provide to help adapt to rising seas, droughts and storms. Talks on a new target stalled last year.",
                    "Negotiators are expected to release a first draft of a final agreement at the end of the first week."
                ]),
                f => new Story("world", "city", "European Central Bank holds rates steady, signals patience",
                    "Policymakers said inflation was moving toward their target but warned against cutting borrowing costs too quickly.",
                [
                    $"FRANKFURT — The European Central Bank left interest rates unchanged on {f.Day} and signaled it was in no hurry to lower them further, even as inflation across the 20 countries that use the euro continued to ease.",
                    $"Consumer prices in the eurozone rose 2.{f.Number % 5 + 1} percent from a year earlier, close to the bank's 2 percent target. Economic growth has been weak, particularly in Germany, the region's largest economy.",
                    $"\"They want to see a few more months of good numbers before they move,\" said {f.Local}, a senior economist at Kessler Wirth Bank. \"Nobody wants to be the central bank that declared victory too soon.\"",
                    "## Markets shrug",
                    "Investors had largely expected the decision. The euro was little changed against the dollar after the announcement, and European stocks ended the day slightly higher."
                ]),
                f => new Story("world", "outdoors", "Rains bring an early end to wildfire season in southern Europe",
                    "Firefighters in Greece, Spain and Portugal reported far fewer large blazes than last year.",
                [
                    $"ATHENS — Heavy autumn rains have brought an early end to the wildfire season across much of southern Europe, officials said {f.Day}, after a summer in which Greece, Spain and Portugal recorded far fewer large blazes than a year earlier.",
                    $"The area burned across the European Union was down about {f.Number % 30 + 30} percent from last year, according to preliminary satellite estimates.",
                    $"\"We were lucky with the weather, but we were also better prepared,\" said {f.Local}, a civil protection official in Athens. \"More crews, earlier patrols, faster response.\"",
                    "## A trend that has not changed",
                    "Scientists cautioned that one milder season did not change the longer-term trend toward hotter, drier summers around the Mediterranean, and urged governments to keep investing in prevention."
                ])
            ],
            "Politics" =>
            [
                f => new Story("civic", "city", $"{city} council approves ${f.Number + 140} million budget after late-night session",
                    "The spending plan adds police and fire positions and a road repair fund, but leaves a proposed library expansion for next year.",
                [
                    $"{at}The {city} City Council approved a ${f.Number + 140} million budget for the coming fiscal year shortly before midnight {f.Day}, after nearly five hours of public comment and debate.",
                    "The plan, approved 6-3, adds eight police and fire positions, sets aside money for a multiyear road repair program and gives city employees a 3 percent raise. It does not raise the property tax rate.",
                    $"\"Nobody got everything they wanted, which is usually how you know it's a fair budget,\" said council President {f.Local}.",
                    "## Opponents warn of a gap next year",
                    $"The three members who voted no said the plan relied too heavily on one-time money. \"We are paying for recurring costs with savings we won't have next year,\" said council member {f.Local2}.",
                    "A proposed expansion of library hours, which drew dozens of supporters to the meeting, was pushed to next year's budget discussions.",
                    "The budget takes effect July 1."
                ]),
                f => new Story("civic", "community", "County commissioners split over road repair funding",
                    "A proposal to borrow $30 million for repaving drew support from rural members and skepticism from the board's chair.",
                [
                    $"{at}County commissioners clashed {f.Day} over a proposal to borrow $30 million to repave some of the county's worst roads, with members from rural districts pressing for a vote this year and the board's chair urging caution.",
                    $"A consultant's study released this summer rated about {f.Number % 20 + 20} percent of county-maintained roads in poor condition, up from 14 percent five years ago.",
                    $"\"People in my district are driving on roads that were last paved when I was in high school,\" said Commissioner {f.Local}. \"Every year we wait, the repairs cost more.\"",
                    "## Chair wants a smaller first step",
                    $"Commissioner {f.Local2}, the board's chair, said {L(f.Local2)} supported more money for roads but worried about taking on debt. \"I want to fix the roads. I don't want to hand the bill to our kids,\" {L(f.Local2)} said.",
                    "The board is expected to take up a revised proposal at its meeting next month."
                ]),
                f => new Story("civic", "community", $"Turnout expected to climb in {city}'s special election",
                    "Early voting is running ahead of the last two municipal elections, with a school bond and an open council seat on the ballot.",
                [
                    $"{at}Early voting in {city}'s special election is running well ahead of recent municipal contests, election officials said {f.Day}, with a $48 million school bond and an open council seat drawing voters to the polls.",
                    $"More than {f.Number * 60 + 3000:N0} ballots had been cast by mail or in person through {f.Day}, compared with about 2,100 at the same point in the last special election.",
                    $"\"The school bond is driving a lot of it,\" said {f.Local}, the county's elections director. \"Parents are paying attention.\"",
                    "## What is on the ballot",
                    "The bond would pay for a new middle school and repairs to three elementary schools. Four candidates are running for the open council seat, which was vacated in the spring.",
                    $"\"The buildings my grandkids go to school in are older than I am,\" said {f.Local2}, a retired teacher who voted early at the downtown library.",
                    "Polls are open from 7 a.m. to 8 p.m. on Election Day. Voters can look up their polling place on the county elections website."
                ])
            ],
            "Business" =>
            [
                f => new Story("org", "office", $"{org} reports steady quarter as costs rise",
                    "Revenue grew modestly, but higher spending on staff and technology trimmed margins, executives told the board.",
                [
                    $"{at}{org} reported revenue of ${f.Number + 80} million for its most recent quarter, up about {f.Number % 6 + 2} percent from a year earlier, as growth in its core business offset rising costs for staff, equipment and technology.",
                    $"Operating costs rose faster than revenue, trimming margins for a second straight quarter. {Of(cfo)}, said much of the increase reflected one-time spending on {world.Projects[0].ToLowerInvariant()}.",
                    $"\"We are spending now so that we spend less later,\" {cfo.LastName} said. \"Most of these costs roll off by the end of next year.\"",
                    "## No layoffs planned",
                    $"In a memo to employees, {Of(ceo)}, said the {noun} had no plans for layoffs. \"Our {customers} count on us, and that does not change because a quarter was a little tighter than we'd like,\" {ceo.LastName} wrote."
                ]),
                f => new Story("org", "office", $"{org} names {f.Person.FullName} {f.Person.Title.ToLowerInvariant()}",
                    "The appointment caps a leadership shuffle that began earlier this year.",
                [
                    $"{at}{org} has named {f.Person.FullName} as its {f.Person.Title.ToLowerInvariant()}, the {noun} announced {f.Day}, filling a role that had been held on an interim basis since spring.",
                    $"{f.Person.LastName} joined {brand} {f.Number % 9 + 3} years ago and most recently ran the day-to-day work of its {f.Person.Department.ToLowerInvariant()} group. {f.Person.LastName} will report to {ceo.FullName}, the {ceo.Title.ToLowerInvariant()}.",
                    $"\"{f.Person.FirstName} knows this place inside and out, and has the trust of the people who do the work every day,\" {ceo.LastName} said in a statement.",
                    "## Priorities for the new role",
                    $"\"I'm grateful for the chance, and I'm excited about what we can do next,\" {f.Person.LastName} said. \"My first priority is listening, to our team and to our {customers}.\"",
                    $"The appointment is effective immediately. {brand} said it does not plan to fill the interim role."
                ]),
                f => new Story("org", "office", $"{org} moves ahead with {world.Projects[f.Number % world.Projects.Count].ToLowerInvariant()} plan",
                    "The multiyear effort is one of the largest the organization has taken on, with a price tag in the tens of millions.",
                [
                    $"{at}{org} is moving ahead with its {world.Projects[f.Number % world.Projects.Count].ToLowerInvariant()} program, a multiyear effort executives say will replace aging systems and make it easier for {customers} to do business with the {noun}.",
                    $"The board approved the first phase this month at a cost of about ${f.Number % 20 + 8} million. Work will be done in stages over three years, with {world.Vendors[0]} as the lead contractor.",
                    $"\"Some of what we run today was installed before smartphones existed,\" said {Of(cio)}. \"This is how we make sure it keeps working for the next 20 years.\"",
                    "## Little disruption expected",
                    $"{cio.LastName} said {customers} should not notice disruptions during the work, which will be scheduled overnight and on weekends where possible."
                ]),
                f => new Story("civic", "community", $"Small employers in {city} expand hiring as demand picks up",
                    "Restaurants, contractors and health providers report the most openings, according to a chamber of commerce survey.",
                [
                    $"{at}Small businesses in {city} are hiring at the fastest pace in two years, according to a survey released {f.Day} by the {city} Area Chamber of Commerce, with restaurants, contractors and health care providers reporting the most openings.",
                    $"About {f.Number % 30 + 40} percent of the roughly 300 businesses surveyed said they planned to add staff in the next six months. Fewer than one in ten expected to cut jobs.",
                    $"\"Finding people is still the hard part,\" said {f.Local}, who owns a heating and cooling company on the east side. \"I could hire three technicians tomorrow if I could find them.\"",
                    "## Training programs expand",
                    $"The chamber's president, {f.Local2}, said the group was working with the community college to expand training in skilled trades. \"The jobs are here. We need to help people get ready for them,\" {L(f.Local2)} said.",
                    "The full survey results are available on the chamber's website."
                ])
            ],
            "Technology" =>
            [
                f => new Story("org", "tech", $"{org} restores {ServiceShort} after network outage",
                    $"{Cap(customers)} were unable to log in for much of {f.Day} while crews worked to restore systems at the {noun}'s {city} headquarters.",
                [
                    $"{at}{org} said {f.Day} evening that it had restored {Service} after a network outage left {customers} unable to log in or reach its call center for much of the day.",
                    $"The problem began shortly after 7 a.m. and was traced to equipment in the data center at the {noun}'s {city} headquarters, according to {Of(sp)}. Service was fully restored by about 5 p.m.",
                    $"\"We know how frustrating this was, and we're sorry,\" {sp.LastName} said. \"There is no indication that any customer information was affected. Our teams are reviewing what happened so we can keep it from happening again.\"",
                    "## A backup that did not take over",
                    $"{Of(cio)}, said a backup system did not take over as designed. \"That's the part we're focused on,\" {cio.LastName} said.",
                    $"{f.Local}, who runs a small bookkeeping business in {city}, said the outage came at a bad time. \"I had three clients waiting on statements,\" {L(f.Local)} said. \"I just kept hitting refresh.\""
                ]),
                f => new Story("org", "tech", $"{org} reminds {customers} it will never ask for passwords by email",
                    $"The notice follows reports of messages that imitate the {noun}'s logo and ask recipients to confirm their account details.",
                [
                    $"{at}{org} is reminding {customers} that it will never ask for a password, PIN or full account number by email or text message, after several people reported receiving messages that imitated the {noun}'s logo.",
                    $"The messages asked recipients to \"confirm account details\" through a link to a look-alike website. {Of(sp)}, said the {noun}'s own systems were not involved.",
                    $"\"If something looks off, don't click. Call us at the number on your statement,\" {sp.LastName} said.",
                    "## How to tell a real message",
                    $"Messages from {brand} come only from addresses ending in {world.Domain}, the {noun} said. Anyone who entered information on a suspicious site should change their password and contact customer service.",
                    $"\"My mother almost clicked it,\" said {f.Local}, a {city} resident who forwarded one of the messages to the {noun}. \"It looked exactly like the real thing.\""
                ]),
                f => new Story("civic", "tech", $"{city} schools roll out new laptops to {f.Number * 50 + 2000:N0} students",
                    "The district is replacing devices that were bought in a hurry during the pandemic and are now wearing out.",
                [
                    $"{at}Students in {city}'s public schools are getting new laptops this month, as the district replaces {f.Number * 50 + 2000:N0} devices that were bought in a rush during the pandemic and have been breaking down at a growing rate.",
                    "The $3.1 million purchase, approved by the school board in June, includes protective cases, extended warranties and a repair program run by students at the district's career center.",
                    $"\"About a third of the old ones were held together with tape,\" said {f.Local}, the district's technology director. \"Kids can't learn on a device that won't hold a charge.\"",
                    "## Families sign new agreement",
                    $"Parents will be asked to sign a new use agreement. {f.Local2}, whose two children attend {city} Middle School, said the rollout had gone smoothly. \"They came home with them on the first day,\" {L(f.Local2)} said."
                ]),
                f => new Story("civic", "office", "Small businesses urged to test their backups before they need them",
                    "Local IT providers say many owners have backups that have never been tried, until the day something breaks.",
                [
                    $"{at}When the server at {f.Local}'s dental practice failed last spring, the office discovered its nightly backup had not run in four months.",
                    $"\"We lost two weeks of scheduling and had to rebuild it from paper,\" {L(f.Local)} said. \"I had no idea.\"",
                    $"Stories like that are common, according to IT providers in the {city} area, who say many small businesses pay for backups but rarely check whether they can actually be restored.",
                    "## A ten-minute habit",
                    $"\"A backup you've never restored is a guess,\" said {f.Local2}, who runs a managed IT firm downtown. \"Pick one file a month and bring it back. It takes ten minutes.\"",
                    "Experts also recommend keeping at least one copy offline or with a different provider, and writing down who to call when something goes wrong."
                ])
            ],
            "Science" =>
            [
                f => new Story("science", "wildlife", "Researchers track record bird migration over the lake",
                    "Radar and volunteer counts show more birds moving through the region this season than in any year on record.",
                [
                    $"{at}More birds passed over the lake this migration season than in any year since researchers began keeping consistent counts, according to preliminary figures released {f.Day} by the regional university's ornithology lab.",
                    $"Radar data and volunteer spotters logged an estimated {f.Number % 5 + 2}.{f.Number % 10} million birds over the season, led by warblers, sparrows and thrushes.",
                    $"\"We had nights when the radar looked like a storm system, and it was all birds,\" said {f.Local}, who leads the lab's migration project.",
                    "## One good year is not a trend",
                    "Researchers said favorable winds and a strong breeding season in the north likely explain most of the jump. They cautioned that many species have declined sharply over the past half-century.",
                    $"Volunteer {f.Local2} has counted birds from the same hilltop every fall for 22 years. \"Some mornings this year, I couldn't write fast enough,\" {L(f.Local2)} said."
                ]),
                f => new Story("science", "community", $"{city} students sweep regional science fair",
                    "Projects on water filtration, battery recycling and bee behavior took top honors, and three teams advance to the state competition.",
                [
                    $"{at}Students from {city} took the top three prizes at the regional science and engineering fair over the weekend, beating more than 200 entries from 30 schools.",
                    "The first-place project, a low-cost water filter made from sand, charcoal and recycled bottles, was designed by a pair of high school juniors. Second place went to a study of battery recycling, and third to an experiment on how bees respond to different flower colors.",
                    $"\"They did every part of it themselves, the research, the testing, the failures,\" said {f.Local}, a chemistry teacher who advised two of the winning teams.",
                    "## On to the state fair",
                    "The top three projects advance to the state fair in the spring. Winners also received scholarships from local businesses.",
                    $"\"Our first filter didn't work at all,\" said {f.Local2}, one of the winners. \"We had to rebuild it four times.\""
                ]),
                f => new Story("science", "wildlife", "Study finds river water quality improving after a decade of cleanup",
                    "Bacteria levels and runoff from farms have dropped, though researchers say the river is not yet safe for swimming after heavy rain.",
                [
                    $"{at}Water in the river that runs through {city} is cleaner than it has been in decades, according to a study released {f.Day}, which credits a decade of sewer upgrades, stream restoration and changes in farm practices upstream.",
                    $"Bacteria levels fell by about {f.Number % 30 + 40} percent at monitoring stations downtown, and phosphorus from fertilizer runoff dropped by about a third.",
                    $"\"Ten years ago, we wouldn't have let our own kids wade in it,\" said {f.Local}, a researcher who led the study. \"Now, on most days, the numbers look good.\"",
                    "## Stay out after storms",
                    "The researchers cautioned that bacteria still spike after heavy rain, when storm sewers overflow, and recommended staying out of the water for 48 hours after a storm.",
                    "The full study and monitoring data are posted on the university's website."
                ])
            ],
            "Health" =>
            [
                f => new Story("health", "health", $"{city} clinics extend weekend hours as respiratory cases climb",
                    "Health officials urged residents to get vaccinated and to stay home when sick.",
                [
                    $"{at}Several clinics in {city} are adding weekend hours as cases of flu, COVID-19 and other respiratory illnesses climb earlier than usual, county health officials said {f.Day}.",
                    $"Emergency room visits for respiratory symptoms rose about {f.Number % 20 + 15} percent over the past two weeks, according to county data. Young children and adults over 65 account for most hospital admissions.",
                    $"\"We're seeing what we usually see in the middle of winter, and it's not winter yet,\" said Dr. {f.Local}, the county's health officer.",
                    "## Where to go",
                    "The extended hours run Saturdays and Sundays from 9 a.m. to 3 p.m. at three locations. Walk-ins are accepted, and vaccines are available at all three.",
                    $"Dr. {f.Local2}, a pediatrician, said parents should watch for trouble breathing and signs of dehydration. \"Most kids will be fine with rest and fluids, but don't wait if they're struggling to breathe,\" {L(f.Local2)} said."
                ]),
                f => new Story("health", "outdoors", "New walking trails open along the river",
                    "The 4-mile loop connects two parks and includes accessible boardwalks through the wetlands.",
                [
                    $"{at}A new 4-mile network of walking and biking trails opened along the river {f.Day}, linking two of {city}'s largest parks and adding boardwalks through wetlands that had been off-limits to the public.",
                    "The trails are paved or boardwalked for their entire length and are accessible to wheelchairs and strollers. Benches, water fountains and mile markers line the route.",
                    $"\"We wanted something you could do on your lunch break or with a toddler,\" said {f.Local}, the city's parks director.",
                    "## A prescription for a walk",
                    $"Doctors at the county health department have begun writing \"park prescriptions\" that encourage patients to walk the trails. \"Thirty minutes a day does more than most pills I can prescribe,\" said Dr. {f.Local2}.",
                    "The trails are open from dawn to dusk. Parking is available at both parks."
                ]),
                f => new Story("health", "community", "Hospital volunteers honored for decades of service",
                    "Some have given more than 10,000 hours, pushing wheelchairs, running the gift shop and sitting with patients who have no visitors.",
                [
                    $"{at}When {f.Local} started volunteering at {city} General Hospital, the gift shop took only cash and the building had one elevator. Forty-one years later, {L(f.Local)} still works the front desk every Tuesday.",
                    $"{L(f.Local)} was one of {f.Number % 20 + 30} volunteers honored at a luncheon {f.Day} for 20 years of service or more. Together they have given more than 150,000 hours.",
                    $"\"People think I do it for them, but honestly, I do it for me,\" {L(f.Local)} said. \"I get more out of it than anybody.\"",
                    "## Filling the gaps",
                    $"Hospital president {f.Local2} said volunteers fill gaps the staff cannot. \"They are the first face people see when they walk in, often on the worst day of their lives,\" {L(f.Local2)} said.",
                    "The hospital is recruiting new volunteers and holds an orientation on the first Saturday of each month."
                ])
            ],
            "Sports" =>
            [
                f => new Story("sports", "fans", $"{f.Team.Full} {Verb(f)} {f.Other.Full} {f.Score}-{f.OtherScore}",
                    $"{f.Star} threw for {f.Number + 180} yards and two touchdowns before a sellout crowd.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star} threw for {f.Number + 180} yards and two touchdowns, and the {f.Team.Full} beat the {f.Other.Full} {f.Score}-{f.OtherScore} on {f.Day} before a sellout crowd at {f.Team.City} Memorial Stadium.",
                    "The game turned late in the third quarter, when a fumble recovery at midfield set up a short touchdown run that put the home team ahead for good.",
                    $"\"We didn't play clean, but we played hard for four quarters,\" said coach {f.Local}. \"I'll take that every week.\"",
                    "## Penalties hurt the visitors",
                    $"{f.Other.Short} coach {f.Local2} said penalties cost the team two scoring chances. \"You can't give a good team extra plays,\" {L(f.Local2)} said.",
                    $"The {f.Team.Short} host a division rival next week."
                ]),
                f => new Story("sports", "athlete", $"{city} marathon draws a record field",
                    "More than 9,000 runners finished, and organizers added a relay division for the first time.",
                [
                    $"{at}A record {f.Number * 40 + 9000:N0} runners crossed the finish line downtown {f.Day} in the {city} Marathon and its companion half marathon, organizers said.",
                    $"{f.Star} won the marathon in 2 hours 14 minutes, a course record, and {f.Star2} won the half marathon in 1:06.",
                    $"\"The crowd on the last two miles carried me,\" {L(f.Star)} said. \"I wasn't sure my legs had anything left.\"",
                    "## A new relay division",
                    $"Race director {f.Local} said a new relay division drew 160 teams. Several downtown streets were closed until early afternoon.",
                    "Registration for next year's race opens in January."
                ]),
                f => new Story("sports", "fans", $"{f.Team.Full} name {f.Star} head coach",
                    "The former defensive coordinator takes over a team that missed the playoffs last season.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — The {f.Team.Full} named {f.Star} as head coach {f.Day}, promoting the team's defensive coordinator after a search that lasted nearly two months.",
                    $"{L(f.Star)}, 44, has spent five seasons with the team. This year the defense ranked third in the league in points allowed.",
                    $"\"This is the job I've wanted my whole life, and this is where I wanted to do it,\" {L(f.Star)} said at a news conference.",
                    "## Players back the choice",
                    $"General manager {f.Local} said {L(f.Star)} stood out among a dozen candidates. \"The players already trust this staff, and that matters,\" {L(f.Local)} said."
                ])
            ],
            "Arts" =>
            [
                f => new Story("arts", "nightlife", $"{city} film festival announces its lineup",
                    "Opening night features a drama shot partly in the city, and the program includes more than 60 features and shorts.",
                [
                    $"{at}The {city} International Film Festival announced its lineup {f.Day}, with more than 60 features and short films from 18 countries, and an opening-night drama shot partly in {city} last year.",
                    "Screenings will be held at the Orpheum Theater and two other downtown venues over 10 days. Passes go on sale next week.",
                    $"\"We look for films that start conversations,\" said {f.Local}, the festival's artistic director. \"This year, a lot of them are about home: leaving it, finding it, coming back.\"",
                    "## Opening night",
                    $"The opening film, '{f.Title}', follows a family that returns to run a struggling diner. Its director, {f.Star}, is expected to attend."
                ]),
                f => new Story("arts", "stage", "Symphony opens its season with a sold-out program",
                    "A new music director made a confident debut, and ticket sales are running ahead of last year.",
                [
                    $"{at}The {city} Symphony opened its season {f.Day} night before a sold-out hall, as new music director {f.Star} led the orchestra through Beethoven, Dvorak and a new work by a local composer.",
                    "Ticket sales are up about 20 percent from last season, the orchestra said, helped by a program that offers $10 seats to anyone under 30.",
                    $"\"There is an energy in the hall right now that you can't fake,\" {L(f.Star)} said after the performance.",
                    "## A longtime subscriber's verdict",
                    $"{f.Local} has held season tickets for 30 years. \"That was the best opening I can remember,\" {L(f.Local)} said."
                ]),
                f => new Story("arts", "art", "Mural project brings color to downtown alleys",
                    "Twelve artists are painting walls along three alleys, part of an effort to draw people to the side streets.",
                [
                    $"{at}Twelve artists are spending the month turning three downtown alleys into an outdoor gallery, part of an effort to draw foot traffic to the side streets off Main.",
                    "The project is paid for by a mix of city money and private donations. Building owners agreed to let the artists use their walls for at least five years.",
                    $"\"Alleys are where people take shortcuts and throw out trash,\" said {f.Local}, who organized the project. \"We want them to be somewhere you'd bring a friend.\"",
                    "## The biggest wall",
                    $"Artist {f.Local2} is painting a 60-foot mural of the river. \"It's the biggest thing I've ever done,\" {L(f.Local2)} said. \"I'm learning a lot about ladders.\""
                ])
            ],
            _ =>
            [
                f => new Story("opinion", "city", $"{city} can't keep patching the same roads",
                    "Every year we pay to fill the same potholes. There is a better way to spend the money.",
                [
                    $"Drive down any of {city}'s main streets after a hard winter and you'll find the same potholes in the same places, filled last year, and the year before that.",
                    "We have been treating our roads like a leaky roof we never quite get around to replacing. Every season, crews patch what they can, and every season the patches fail a little faster.",
                    "The city's own consultants have said this approach costs more over time. A road that gets resurfaced on schedule lasts decades. A road that gets patched until it crumbles has to be rebuilt from the base, at several times the cost.",
                    "The council has a chance to change course in this year's budget. A dedicated repair fund, protected from year-to-year raids, would let the city plan work years in advance instead of reacting to complaints.",
                    "No one likes paying for infrastructure. It isn't glamorous, and the benefits show up slowly. But the alternative is what we have now: higher costs, worse roads and a lot of bent rims.",
                    "Fix it once. Fix it right. Then keep it that way."
                ]),
                f => new Story("opinion", "community", "The case for keeping the library open late",
                    "For students, shift workers and new arrivals, the library is the only quiet, warm and free place in town after 6 p.m.",
                [
                    $"On a weeknight at 5:55, the main branch of the {city} library is full. Students are finishing homework, a man is filling out a job application, two kids are reading in the corner while their mother finishes a shift across the street. At 6, everyone has to leave.",
                    "The proposal to extend hours to 9 p.m. on weeknights would cost less than one percent of the city budget. It was cut from this year's plan anyway.",
                    "Critics say people can use the internet at home. Many can't. A quarter of the households near the main branch don't have reliable broadband, according to the city's own survey.",
                    "A library is one of the few public places where nobody has to buy anything to stay. That matters more after dark, when the alternatives are a fast-food restaurant or a parked car.",
                    "The council should restore the money next year. Better yet, it should find it now."
                ]),
                f => new Story("opinion", "tech", $"What the {brand} outage should teach every local institution",
                    "Systems fail. What matters is how quickly people find out what is happening and what to do.",
                [
                    $"When {org}'s systems went down for most of a day, the hardest part for many {customers} wasn't the outage itself. It was not knowing what was going on.",
                    "Every organization that runs on computers, which is every organization, will have a bad day eventually. Hardware fails. Software has bugs. People make mistakes.",
                    "What separates a bad day from a crisis is communication. Post a plain update early, even if it only says what you don't know yet. Then post another one an hour later. Silence fills up with rumors.",
                    "It also helps to test the backup plan before you need it. A backup that has never been switched on is a hope, not a plan.",
                    $"{brand} says it is reviewing what happened. It should share what it learns. So should the rest of us."
                ]),
                f => new Story("opinion", "city", "Our downtown is coming back. Let's not get in its way.",
                    "New shops and restaurants are filling empty storefronts. City Hall's job is to make that easier, not harder.",
                [
                    $"Five years ago, a walk down Main Street in {city} meant counting empty windows. Today there is a bakery, two restaurants, a bike shop and a bookstore where some of those windows used to be.",
                    "That didn't happen because of a master plan. It happened because people with small savings and big ideas took a chance.",
                    "The city can help them by getting out of the way. A new restaurant should not need four months and three separate inspections to open. A sidewalk cafe permit should not cost more than the tables.",
                    "Parking is the other fight. Every study the city has commissioned says there is enough of it downtown. It just isn't free and in front of the door.",
                    "Downtown's comeback is fragile. Let's keep it going."
                ])
            ]
        };
    }

    private Func<Facts, Story>[] SportsStories(string section)
    {
        var org = world.OrganizationName;
        var noun = OrgNoun;
        var (ceo, sp) = (Ceo, Spokesperson);

        if (section == "Golf")
            return
            [
                f => new Story("individual", "course", V(f, $"{f.Star} shoots 64 to take three-shot lead at the {f.Team.City} Classic", $"{f.Star} fires a bogey-free 64, leads {f.Team.City} Classic by three", $"{f.Star} surges to the top at the {f.Team.City} Classic"),
                    $"Eight birdies and no bogeys put {L(f.Star)} in control heading into the weekend.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star} made eight birdies without a bogey on {f.Day}, shooting an 8-under 64 to take a three-shot lead at the {f.Team.City} Classic.",
                    $"{L(f.Star)} is at 13 under for the tournament, three ahead of {f.Star2} and four ahead of a group of five players.",
                    $"\"I was just trying to hit the middle of the greens and let the putter do the work,\" {L(f.Star)} said. \"It was one of those days where everything went in.\"",
                    "## Wind expected for the weekend",
                    "Forecasters are calling for gusts above 20 mph on Saturday, which could bring the field back to the leaders on a course known for its exposed back nine."
                ]),
                f => new Story("individual", "course", V(f, $"{f.Star} wins the {f.Team.City} Classic for a first career title", $"{f.Star} holds off {f.Star2} to win the {f.Team.City} Classic", $"First win at last: {f.Star} takes the {f.Team.City} Classic"),
                    $"A closing 67 held off {f.Star2}, who birdied the last two holes to make it close.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star} closed with a 5-under 67 on {f.Day} to win the {f.Team.City} Classic by two shots, the first tour victory of a career that has included four runner-up finishes.",
                    $"{f.Star2} birdied the final two holes to finish alone in second. Three players tied for third.",
                    $"\"I've been close enough times that I was starting to wonder,\" {L(f.Star)} said. \"To finally get it done here is pretty special.\"",
                    "## A big jump in the standings",
                    $"The win moves {L(f.Star)} into the top 20 of the season standings and earns a spot in next month's invitational."
                ]),
                f => new Story("individual", "athlete", V(f, $"{f.Star2} withdraws from the {f.Team.City} Classic with back spasms", $"Back spasms force {f.Star2} out of the {f.Team.City} Classic", $"{f.Star2} exits {f.Team.City} Classic in the first round"),
                    "The former champion was 2 over through 11 holes when play was stopped.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star2} withdrew from the {f.Team.City} Classic during the first round on {f.Day} with back spasms, tournament officials said.",
                    $"{L(f.Star2)}, a former champion of the event, was 2 over through 11 holes and was seen stretching on several tees before leaving the course in a cart.",
                    $"\"It just grabbed on me on the range this morning,\" {L(f.Star2)} said in a statement. \"I tried to play through it, but it wasn't going to happen.\"",
                    "## Status for next week uncertain",
                    "It was not immediately clear whether the withdrawal would affect plans to play in next week's event."
                ])
            ];

        if (section == "Tennis")
            return
            [
                f => new Story("individual", "athlete", V(f, $"{f.Star} rallies past {f.Star2} to reach the {f.Team.City} Open final", $"{f.Star} comes back to beat {f.Star2}, reaches {f.Team.City} Open final", $"Comeback puts {f.Star} in the {f.Team.City} Open final"),
                    "Down a set and a break, the No. 4 seed won 11 of the last 14 games.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star} came back from a set and a break down to beat {f.Star2} 4-6, 6-3, 6-2 on {f.Day} and reach the final of the {f.Team.City} Open.",
                    $"{L(f.Star)}, the fourth seed, won 11 of the last 14 games and saved all three break points faced in the final set.",
                    $"\"At 4-6, 1-3, I just told myself to make every point long,\" {L(f.Star)} said. \"Eventually the balls started landing.\"",
                    "## A final against the top seed",
                    $"{L(f.Star)} will face the top seed in the final, a rematch of last year's semifinal."
                ]),
                f => new Story("individual", "athlete", V(f, $"{f.Star} beats {f.Star2} for the {f.Team.City} Open title", $"{f.Star} wins the {f.Team.City} Open in straight sets", $"{f.Star} claims first title of the season at the {f.Team.City} Open"),
                    "The straight-sets win is the first title of the season for the 24-year-old.",
                [
                    $"{f.Team.City.ToUpperInvariant()} — {f.Star} beat {f.Star2} 6-4, 7-5 in the final of the {f.Team.City} Open on {f.Day}, winning a first title of the season.",
                    $"{L(f.Star)} broke serve twice and finished with 31 winners. {L(f.Star2)} had won both of their previous meetings.",
                    $"\"I've lost to a lot of good players this year, and I learned something every time,\" {L(f.Star)} said.",
                    "## Up the rankings",
                    $"The win is expected to move {L(f.Star)} into the top 15 when new rankings are released on Monday."
                ]),
                f => new Story("individual", "athlete", V(f, $"{f.Star2} pulls out of the indoor season to rest a shoulder", $"Shoulder injury ends {f.Star2}'s season early", $"{f.Star2} shuts it down for the year to rest a shoulder"),
                    "The former top-10 player said the injury had bothered them since the summer.",
                [
                    $"{f.Star2} will miss the rest of the indoor season to rest an injured shoulder, the player's management team said {f.Day}.",
                    $"{L(f.Star2)} has played through pain since the summer and lost in the first round of three of the last four events.",
                    $"\"I want to come back healthy, not halfway,\" {L(f.Star2)} said in a statement.",
                    "## A return in January",
                    $"{L(f.Star2)} plans to return for the start of next season in January."
                ])
            ];

        var coach = section is "Soccer" or "Baseball" ? "manager" : "coach";
        var sponsor = new Func<Facts, Story>(f => new Story("org", "fans", $"{org} signs naming-rights deal for {Possessive(f.Team.Short)} arena",
            $"The 10-year agreement renames the downtown arena and funds new youth programs.",
        [
            $"{f.Team.City.ToUpperInvariant()} — The home of the {f.Team.Full} has a new name. {org} signed a 10-year naming-rights agreement for the downtown arena, the team announced {f.Day}, and the building will be known as {world.ShortName} Arena starting next season.",
            "Terms were not disclosed, but people familiar with similar deals said they typically run several million dollars a year.",
            $"\"Our employees and {Customers} are fans of this team, and this is a way to give back to the region,\" said {Of(ceo)}.",
            "## Youth programs included",
            $"{Of(sp)}, said the agreement includes a youth sports fund and free tickets for community groups.",
            $"\"Partners who are rooted here matter to us,\" said team president {f.Local}."
        ]));

        Func<Facts, Story>[] stories =
        [
            f => new Story("recap", "match", $"{f.Team.Short} {Verb(f)} {f.Other.Short} {f.Score}-{f.OtherScore}",
                $"{Stat(f)} as {The(f.Team)} won at {Venue(f.Team)}.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {Stat(f)}, and {The(f.Team)} beat {The(f.Other)} {f.Score}-{f.OtherScore} on {f.Day} night at {Venue(f.Team)}.",
                Turn(f),
                $"\"{Q(f, 0, "That's as complete a game as we've played all year. Everybody did their job.", "We found a way, and that's what good teams do.", "I liked our energy from the start. We were the more physical team tonight.", "We made it harder than it needed to be, but I'll take it.").TrimEnd('.')},\" said {f.Team.Short} {coach} {f.Local}.",
                $"\"{Q(f, 1, "I just tried to take what they gave me. The guys around me made it easy.", "It's a team game. I just happened to be in the right spot.", "We talked all week about starting fast. Tonight we did it.").TrimEnd('.')},\" {L(f.Star)} said.",
                "## What went wrong for the visitors",
                $"{f.Other.Short} {coach} {f.Local2} said the team had its chances. \"{Q(f, 2, "We didn't finish them, and against a team like that, you get punished.", "We beat ourselves. That's the frustrating part.", "Give them credit. They made the plays when it mattered.").TrimEnd('.')},\" {L(f.Local2)} said."
            ]),
            f => new Story("team", "athlete", V(f, $"{f.Team.Short} star {f.Star} expected back from {Injury(f)} injury next week", $"{f.Star} cleared to practice, could return for {f.Team.Short} this week", $"Injury update: {f.Star} nearing return for {f.Team.Short}", $"{f.Team.Short} hopeful {f.Star} is back from {Injury(f)} injury soon"),
                $"{L(f.Star)} has missed three games, and the team has gone 1-2 in that stretch.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {f.Team.Full} star {f.Star} is expected to return from a {Injury(f)} injury next week, {coach} {f.Local} said {f.Day}, after missing the past three games.",
                $"{L(f.Star)} was hurt in the first half against {The(f.Other)} and has been limited to individual work since. The team has gone 1-2 without its leading scorer.",
                $"\"Every day's a little better,\" {L(f.Local)} said. \"We're not going to rush it, but we're optimistic.\"",
                "## Itching to get back",
                $"{L(f.Star)} was named player of the week earlier this season. \"I hate watching,\" {L(f.Star)} said. \"I'm ready when they say I'm ready.\""
            ]),
            f => new Story("team", "team", V(f, $"{f.Team.Short} hire {f.Star} as {coach}", $"{f.Team.Short} name {f.Star} {coach} after two-month search", $"{f.Team.Full} tab longtime assistant {f.Star} as next {coach}", $"Sources: {f.Team.Short} finalizing deal to make {f.Star} their {coach}"),
                section == "Soccer" ? "The former assistant takes over a club that finished in the bottom half last season." : "The former assistant takes over a team that missed the playoffs last season.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {Cap(The(f.Team))} hired {f.Star} as {coach} {f.Day}, ending a search that lasted nearly two months and included more than a dozen candidates.",
                $"{L(f.Star)}, 44, spent the past five seasons as an assistant with {The(f.Other)}, building one of the league's best defenses.",
                $"\"This is a place with real expectations, and I wouldn't want it any other way,\" {L(f.Star)} said at an introductory news conference.",
                "## Why the search ended here",
                $"Team president {f.Local} said {L(f.Star)} stood out for preparation and for how players responded. \"Everyone we talked to said the same thing: players love playing for this coach,\" {L(f.Local)} said."
            ]),
            f => new Story("preview", "match", V(f, $"{f.Team.Short} vs. {f.Other.Short}: three things to watch with first place on the line", $"First place on the line as {f.Team.Short} host {f.Other.Short}", $"Why {f.Team.Short}-{f.Other.Short} is the game of the week"),
                "Both teams arrive on winning streaks, and the winner takes sole possession of first place.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {Cap(The(f.Team))} and {The(f.Other)} meet this week at {Venue(f.Team)} with first place on the line, and both arrive on winning streaks.",
                $"## 1. {L(f.Star)} against the best defense in the league",
                $"{f.Star} has been the best player on the field in each of the last three meetings. {Cap(The(f.Other))} have tried everything to slow {L(f.Star)} down, with little success.",
                "## 2. Depth",
                "Both teams are dealing with injuries, and the bench could decide it. The home team's reserves have outscored their opponents in five straight games.",
                "## 3. The crowd",
                $"{Venue(f.Team)} has been sold out for weeks, and {The(f.Team)} have won eight straight at home. \"Our building is loud,\" {coach} {f.Local} said. \"We feed off that.\""
            ]),
            f => new Story("team", "athlete", V(f, $"{f.Star}, {f.Team.Short} agree to four-year, ${f.Number + 40} million extension", $"{f.Team.Short} lock up {f.Star} with ${f.Number + 40} million extension", $"{f.Star} signs ${f.Number + 40} million deal to stay with {f.Team.Short}"),
                "The deal makes the All-Star one of the highest-paid players in team history.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {f.Star} has agreed to a four-year, ${f.Number + 40} million contract extension with {The(f.Team)}, the team announced {f.Day}.",
                $"{L(f.Star)}, 27, was eligible for free agency after the season. The extension makes {L(f.Star)} one of the highest-paid players in team history.",
                $"\"I never wanted to play anywhere else,\" {L(f.Star)} said. \"This city has been good to me, and we have unfinished business.\"",
                "## A cornerstone locked in",
                $"General manager {f.Local} called the deal a priority from the start of the season. \"You build around players like this,\" {L(f.Local)} said."
            ]),
            f => new Story("team", "fans", V(f, $"How {f.Team.City} fell back in love with {The(f.Team)}", $"Sellout streak hits 21 as {f.Team.City} rallies around {The(f.Team)}", $"Inside the turnaround that has {f.Team.City} buzzing about {The(f.Team)}"),
                "Twenty-one straight sellouts, a waiting list for season tickets and a city that can't stop talking about its team.",
            [
                $"{f.Team.City.ToUpperInvariant()} — Three years ago, {The(f.Team)} were giving tickets away. This season, they have sold out 21 straight home games, and the waiting list for season tickets is longer than it has been in a decade.",
                $"\"You can feel it walking around town,\" said {f.Local}, who has held season tickets since the 1990s. \"Everybody's wearing the colors again.\"",
                $"The turnaround began with a young core led by {f.Star}, and a front office that made a point of showing up at schools, hospitals and youth leagues across the region.",
                "## Business is booming",
                $"Bars and restaurants near {Venue(f.Team)} say game nights have become their busiest of the week. \"We staff up like it's a holiday,\" said {f.Local2}, who manages a sports bar two blocks away."
            ])
        ];

        if (section == "College")
            stories[4] = f => new Story("team", "athlete", $"Four-star recruit {f.Star} commits to {f.Team.Full}",
                "The in-state standout chose the program over several larger schools.",
            [
                $"{f.Team.City.ToUpperInvariant()} — {f.Star}, one of the top recruits in the region, committed to the {f.Team.Full} on {f.Day}, choosing the program over offers from several larger schools.",
                $"{L(f.Star)} set school records for passing yards and touchdowns as a junior and was named the state's offensive player of the year.",
                $"\"It felt like home the first time I visited,\" {L(f.Star)} said. \"The coaches told me the truth, and that mattered.\"",
                "## A program on the rise",
                $"Head coach {f.Local} cannot comment on recruits until they sign, but the program has landed three of the region's top 10 prospects this year."
            ]);

        return section == "Basketball" ? [.. stories, sponsor] : stories;
    }

    private Func<Facts, Story>[] EntertainmentStories(string section)
    {
        var city = world.City;
        var org = world.OrganizationName;
        var (ceo, sp) = (Ceo, Spokesperson);
        return section switch
        {
            "Film" =>
            [
                f => new Story("industry", "portrait", V(f, $"{f.Star} to star in '{f.Title}' for {f.Studio}", $"{f.Studio} lands {f.Star} for thriller '{f.Title}'", $"{f.Star} joins {Possessive(f.Studio)} '{f.Title}' in the lead role"),
                    "The thriller, set in a fishing town cut off by a storm, begins shooting in the spring.",
                [
                    $"{f.Star} has signed on to star in '{f.Title}', a thriller from {f.Studio} set in a fishing town where a storm cuts residents off from the mainland, the studio confirmed {f.Day}.",
                    $"{f.Star2} will direct from a script by {f.Local}, whose last screenplay landed on the industry's annual list of the best unproduced scripts. Production begins in the spring.",
                    $"\"I read it on a plane and missed my connection because I couldn't put it down,\" {L(f.Star)} said in a statement.",
                    "## A big year for the studio",
                    $"{f.Studio} president {f.Local2} called the project \"exactly the kind of grown-up thriller audiences tell us they want.\" The studio is aiming for a fall release next year."
                ]),
                f => new Story("industry", "portrait", V(f, $"'{f.Title}' trailer: first look at {f.Star} in {Possessive(f.Studio)} new drama", $"Watch: {f.Star} goes home again in the first '{f.Title}' trailer", $"First '{f.Title}' teaser puts {f.Star} front and center"),
                    "The two-minute teaser racked up millions of views in its first day.",
                [
                    $"The first trailer for '{f.Title}' arrived {f.Day}, and it puts {f.Star} front and center as a detective who returns to a hometown that would rather forget.",
                    $"The two-minute teaser, released by {f.Studio}, had more than {f.Number % 9 + 4} million views within 24 hours.",
                    $"Director {f.Star2} said the cast spent weeks in a small coastal town to get the details right. \"We wanted you to smell the salt,\" {L(f.Star2)} said.",
                    "## When you can see it",
                    $"'{f.Title}' opens in theaters next spring before streaming on {f.Network}."
                ]),
                f => new Story("org", "nightlife", $"{org} named presenting sponsor of the {city} International Film Festival",
                    "The three-year deal funds free student screenings and a new award for first-time filmmakers.",
                [
                    $"{city.ToUpperInvariant()} — {org} will be the presenting sponsor of the {city} International Film Festival for the next three years, festival organizers announced {f.Day}.",
                    "The agreement pays for free screenings for high school and college students and creates a new prize for first-time directors.",
                    $"\"This festival brings the world to {city}, and we're proud to be part of it,\" said {Of(ceo)}.",
                    "## A new award for first films",
                    $"{Of(sp)}, said employees will also volunteer at the festival. Artistic director {f.Local} said the new award will be presented on closing night."
                ])
            ],
            "TV" =>
            [
                f => new Story("industry", "portrait", V(f, $"'{f.Title}' renewed for Season 2 at {f.Network}", $"{f.Network} orders a second season of '{f.Title}'", $"It's official: '{f.Title}' is coming back for Season 2"),
                    "The drama was the platform's most-watched new series of the year.",
                [
                    $"{f.Network} has renewed '{f.Title}' for a second season, the streamer announced {f.Day}, after the drama became its most-watched new series of the year.",
                    $"The first season, led by {f.Star} and {f.Star2}, was watched by about {f.Number % 20 + 18} million households in its first month, according to {f.Network}.",
                    $"\"We knew we had something special, but the response has been overwhelming,\" said creator {f.Local}. \"We have a lot more story to tell.\"",
                    "## What we know about Season 2",
                    $"Production begins early next year. {L(f.Star)} and {L(f.Star2)} are expected to return, and {f.Network} said new cast members will be announced in the coming months."
                ]),
                f => new Story("industry", "portrait", V(f, $"'{f.Title}' star {f.Star} breaks down that finale twist", $"{f.Star} on the '{f.Title}' finale: 'I had to go for a walk'", $"'{f.Title}' showrunner explains the finale everyone is talking about"),
                    $"Spoilers ahead: {L(f.Star)} says the cast didn't know the ending until the final table read.",
                [
                    $"Warning: this story contains spoilers for the season finale of '{f.Title}'.",
                    $"If you didn't see that ending coming, neither did {f.Star}. The actor said the cast found out about the finale twist at the last table read of the season.",
                    $"\"There was a gasp in the room, and then everyone looked at me,\" {L(f.Star)} said. \"I had to go for a walk.\"",
                    "## Where the story goes next",
                    $"Showrunner {f.Local} said the twist had been planned since the pilot. \"Every clue is there if you go back and watch,\" {L(f.Local)} said. '{f.Title}' streams on {f.Network}."
                ]),
                f => new Story("industry", "portrait", V(f, $"{f.Network} cancels '{f.Title}' after one season", $"'{f.Title}' won't return for a second season at {f.Network}", $"Fans rally after {f.Network} pulls the plug on '{f.Title}'"),
                    "Fans had launched a campaign to save the comedy, which drew strong reviews but small audiences.",
                [
                    $"{f.Network} has canceled '{f.Title}' after one season, the streamer confirmed {f.Day}, despite strong reviews and a vocal fan campaign.",
                    $"The comedy, starring {f.Star}, drew a loyal following but never broke into the platform's top 10.",
                    $"\"We're heartbroken, but we're proud of every episode,\" {L(f.Star)} wrote on social media. \"Thank you for watching.\"",
                    "## Could it find a new home?",
                    $"Executive producer {f.Local} said the team would shop the show to other outlets. Fans have already started a petition."
                ])
            ],
            "Music" =>
            [
                f => new Story("music", "studio", V(f, $"New album '{f.Album}' from {f.Artist} arrives in March, with a spring tour", $"Everything we know about '{f.Album}', the new album from {f.Artist}", $"First single from {Possessive(f.Artist)} '{f.Album}' is here, and a tour is coming"),
                    "The first single is out now, and tickets for the 30-city tour go on sale next week.",
                [
                    $"A new album is on the way from {f.Artist}. '{f.Album}', the first full-length record in three years, arrives in March, the label announced {f.Day}.",
                    $"The first single dropped at midnight and passed {f.Number % 8 + 2} million streams in its first day.",
                    $"\"This one was written in a cabin with no cell service, and you can hear it,\" {f.Artist} said in a statement. \"It's the most honest thing we've made.\"",
                    "## Tour dates",
                    $"A 30-city tour begins in April, with a stop in {city} in May. Tickets go on sale next week."
                ]),
                f => new Story("music", "stage", V(f, $"Second {city} show added for {f.Artist} after first date sells out in minutes", $"Missed out on {f.Artist} tickets? A second {city} show just went on sale", $"{f.Artist} tickets: new {city} date added after instant sellout"),
                    "Fans who missed out the first time get another chance on Friday.",
                [
                    $"Good news for fans who missed out: {f.Artist} has added a second {city} show after the first sold out in under 10 minutes, promoters said {f.Day}.",
                    $"The new date is the night after the original show at the {city} Amphitheater. Tickets go on sale Friday at 10 a.m.",
                    $"\"We saw the numbers and said, we have to come back for another night,\" {f.Artist} said.",
                    "## Watch out for resale prices",
                    $"Resale listings for the first show topped $400. Promoter {f.Local} urged fans to buy only through the official box office."
                ]),
                f => new Story("scene", "records", V(f, $"Vinyl sales hit a 30-year high, and {city}'s record shops are feeling it", $"Why {city}'s record stores are having their best year in decades", $"Young buyers are fueling a vinyl boom at {city} record shops"),
                    "Independent stores say younger buyers are driving the boom.",
                [
                    $"Vinyl records outsold CDs again last year, reaching their highest sales in three decades, and independent shops in {city} say they can barely keep the bins full.",
                    $"\"Half my customers are under 25,\" said {f.Local}, who has owned a record store downtown for 18 years. \"They want something they can hold.\"",
                    $"Releases from artists like {f.Artist} regularly sell out on preorder, store owners said, and pressing plants are booked months in advance.",
                    "## Prices are climbing too",
                    $"New records now often cost $35 or more. \"People still buy them,\" said {f.Local2}, a clerk at another shop. \"They just buy fewer.\""
                ])
            ],
            "Celebrity" =>
            [
                f => new Story("celebrity", "portrait", V(f, $"{f.Star} and {f.Star2} make red carpet debut as a couple", $"It's official: {f.Star} and {f.Star2} step out together", $"{f.Star} and {f.Star2} go public at the '{f.Title}' premiere"),
                    "The pair, first linked in the spring, posed together at a premiere on Thursday night.",
                [
                    $"It's official. {f.Star} and {f.Star2} made their red carpet debut as a couple at the premiere of '{f.Title}', posing together and holding hands for photographers.",
                    $"The pair were first spotted together in the spring, but had kept their relationship out of the spotlight until now.",
                    $"\"They're really happy and finally ready to stop hiding,\" a source close to the couple told us.",
                    "## How they met",
                    $"{L(f.Star)} and {L(f.Star2)} met on the set of a film that has not yet been released. Representatives for both did not respond to requests for comment."
                ]),
                f => new Story("celebrity", "portrait", V(f, $"{f.Star} spotted house-hunting in {city}", $"Is {f.Star} moving to {city}? The star was seen touring homes", $"{f.Star} tours historic homes in {city}, sparking move rumors"),
                    "The star toured several homes in one of the city's oldest neighborhoods.",
                [
                    $"Is {f.Star} moving to {city}? The star was spotted touring several homes in one of the city's oldest neighborhoods this week.",
                    $"Neighbors said {L(f.Star)} arrived with a real estate agent and spent about an hour at a restored Victorian with a wraparound porch.",
                    $"\"I was walking my dog and did a double take,\" said {f.Local}, who lives on the street. \"Very friendly. Waved at everyone.\"",
                    "## Why here?",
                    $"{L(f.Star)} has talked about wanting a quieter life away from Los Angeles. A representative did not respond to a request for comment."
                ]),
                f => new Story("celebrity", "portrait", V(f, $"{f.Star} on quitting social media: 'I got my weekends back'", $"Why {f.Star} deleted every social media account", $"{f.Star} says life is better offline: 'I read books again'"),
                    "The actor deleted every account last year and says there are no plans to return.",
                [
                    $"{f.Star} hasn't posted anything in more than a year, and says that won't change any time soon.",
                    $"\"I got my weekends back,\" {L(f.Star)} said in a new interview. \"I read books again. I call my friends instead of watching their stories.\"",
                    $"{L(f.Star)}, who once had more than {f.Number % 20 + 5} million followers, said the decision came after a stretch of online harassment.",
                    "## Advice for fans",
                    $"\"Take a week off and see how you feel,\" {L(f.Star)} said. \"Worst case, it's all still there when you get back.\""
                ])
            ],
            "Streaming" =>
            [
                f => new Story("industry", "streaming", V(f, $"'{f.Title}' tops the streaming charts for a third straight week", $"'{f.Title}' is still the most-watched show in the country", $"How '{f.Title}' became the streaming hit nobody saw coming"),
                    $"The {f.Network} series beat out several big-budget newcomers.",
                [
                    $"'{f.Title}' was the most-streamed series in the country for the third straight week, according to ratings data released {f.Day}.",
                    $"The {f.Network} drama was watched for about {f.Number % 900 + 900} million minutes, ahead of several newer and more expensive shows.",
                    $"\"Word of mouth is doing the work,\" said {f.Local}, an analyst who tracks streaming viewership. \"People finish it and tell their friends.\"",
                    "## The rest of the top 10",
                    $"A true-crime documentary and a cooking competition rounded out the top three. '{f.Title}' stars {f.Star}."
                ]),
                f => new Story("industry", "streaming", V(f, "What to watch this weekend: 9 new movies and shows to stream", $"New on streaming this week: '{f.Title}', a baking show and more", $"Your weekend watch list: '{f.Title}' leads the new arrivals"),
                    "A twisty thriller, a cozy baking competition and the return of a fan favorite.",
                [
                    "Not sure what to watch? Here are our picks for the best new movies and shows arriving on streaming services this weekend.",
                    $"## '{f.Title}' ({f.Network})",
                    $"{f.Star} leads this twisty thriller about a family that inherits a house nobody wants to sell. Start it Friday night and you'll finish it by Sunday.",
                    "## 'The Great Valley Bake-Off'",
                    "The cozy competition is back for a fourth season, with a new judge and the same impossible showstoppers.",
                    $"## 'Night Shift' (Season 3)",
                    $"The hospital drama returns with its strongest episodes yet, according to early reviews."
                ]),
                f => new Story("industry", "streaming", V(f, $"{f.Network} raises subscription prices for the second time this year", $"{f.Network} price hike: the ad-free plan is going up $2", $"Your {f.Network} bill is going up again"),
                    "The ad-free plan will cost $2 more a month starting next billing cycle.",
                [
                    $"{f.Network} is raising prices again. The streamer said {f.Day} that its ad-free plan will cost $2 more a month starting with subscribers' next billing cycle.",
                    "The plan with ads will stay at its current price, a move analysts said is meant to push subscribers toward the cheaper tier.",
                    $"\"Every service is trying to get people onto the ad plans,\" said {f.Local}, a media analyst. \"That's where the money is now.\"",
                    "## Will people cancel?",
                    $"Past price increases led to a small bump in cancellations that faded within a few months, {L(f.Local)} said."
                ])
            ],
            "Box Office" =>
            [
                f => new Story("industry", "nightlife", V(f, $"Box office: '{f.Title}' opens to ${f.Number + 30} million, topping forecasts", $"'{f.Title}' wins the weekend with ${f.Number + 30} million debut", $"'{f.Title}' beats expectations with a ${f.Number + 30} million opening"),
                    "The original drama beat expectations by nearly $10 million.",
                [
                    $"'{f.Title}' opened to an estimated ${f.Number + 30} million in North America this weekend, well ahead of forecasts and the best start for an original film this year.",
                    $"The {f.Studio} release, starring {f.Star}, drew strong reviews and an A- grade from audiences polled on opening night.",
                    $"\"Original movies can still open,\" said {f.Local}, a box office analyst. \"You just have to give people a reason to leave the house.\"",
                    "## The rest of the top five",
                    "Last week's No. 1 slipped to second, and a family animated film held steady in third."
                ]),
                f => new Story("industry", "nightlife", V(f, $"'{f.Title}' holds No. 1 in a quiet weekend at the box office", $"Box office: '{f.Title}' repeats at No. 1", $"'{f.Title}' stays on top as no new wide releases open"),
                    "No new wide releases opened, leaving the drama at the top for a second week.",
                [
                    $"'{f.Title}' stayed at No. 1 for a second straight weekend, adding an estimated ${f.Number % 20 + 12} million in a quiet frame with no new wide releases.",
                    $"The film's second-weekend drop of about 38 percent was among the smallest of the year for a major release, a sign of strong word of mouth.",
                    $"\"Holding like that means people are recommending it,\" said {f.Local}, an analyst who tracks ticket sales.",
                    "## Coming next weekend",
                    "Three new films open next weekend, including a sequel to last summer's biggest hit."
                ]),
                f => new Story("industry", "nightlife", V(f, "Theater owners bet on premium screens as audiences return", "Bigger screens, bigger prices: why theaters are going premium", "Premium screens are driving a box office comeback"),
                    "Bigger screens, reclining seats and better sound are pulling in moviegoers willing to pay more.",
                [
                    "Movie theaters are spending big on premium screens, and the bet appears to be paying off.",
                    $"Large-format and premium screens account for about {f.Number % 10 + 12} percent of screens but nearly a quarter of ticket sales, according to industry data.",
                    $"\"People will pay more for a night out that feels like an event,\" said {f.Local}, who runs a chain of theaters in the Midwest.",
                    "## A boost from blockbusters",
                    $"Releases like '{f.Title}' have sold out premium shows weeks in advance, theater owners said."
                ])
            ],
            "Reviews" =>
            [
                f => new Story("review", "portrait", V(f, $"'{f.Title}' review: {f.Star} is magnetic in a slow-burn thriller", $"'{f.Title}' review: {f.Star} anchors a gripping, patient thriller", $"Review: '{f.Title}' is the slow-burn thriller of the season"),
                    "The story takes its time, but the payoff is worth the wait.",
                [
                    $"The first half hour of '{f.Title}' is so quiet you might wonder if anything is going to happen. Stay with it.",
                    $"{f.Star} plays a ferry captain who finds something in the water that the rest of the town would rather not discuss, and the performance is a master class in saying a lot with very little.",
                    $"Director {f.Star2} lets scenes run long, and not all of them earn it. But when the film finally tightens its grip, in a storm-lashed final act, it does not let go.",
                    "## The verdict",
                    $"'{f.Title}' is not for viewers who want a twist every 10 minutes. For everyone else, it is one of the year's most absorbing thrillers. Grade: B+"
                ]),
                f => new Story("review", "portrait", V(f, $"'{f.Title}' review: a warm, messy family comedy that earns its tears", $"'{f.Title}' review: a big, warm wedding comedy with heart", $"Review: '{f.Title}' is crowded, messy and hard not to love"),
                    "A sprawling ensemble and a sharp script make up for a baggy middle.",
                [
                    $"Every family has a wedding like the one in '{f.Title}': too many relatives, not enough chairs and at least one secret that will come out before the cake is cut.",
                    $"{f.Star} anchors a sprawling cast as the bride's older sibling, the one who always holds things together, until they don't.",
                    "The middle stretch sags, and a subplot about a lost dog goes nowhere. But the script is sharp, the cast clearly likes each other, and the final scene lands hard.",
                    "## The verdict",
                    $"Bring tissues and a forgiving attitude toward the runtime. Grade: B"
                ]),
                f => new Story("review", "records", V(f, $"'{f.Album}' review: the boldest record yet from {f.Artist}", $"Review: '{f.Album}' is big, messy and impossible to ignore", $"'{f.Album}' album review: big swings and the song of the year"),
                    "Big swings, a few misses and the best song of the year.",
                [
                    $"'{f.Album}', the new record from {f.Artist}, opens with six minutes of strings and static before the first word is sung. It is a statement of intent.",
                    "The album swings between hushed, almost whispered ballads and the loudest songs of a long career. Not every experiment works, and a pair of tracks in the middle feel unfinished.",
                    "But the closing song, built on a single piano line and a choir recorded in a church basement, is the best thing anyone has released this year.",
                    "## The verdict",
                    $"Messy, ambitious and impossible to ignore. Grade: A-"
                ])
            ],
            _ =>
            [
                f => new Story("awards", "stage", V(f, $"{city} Film Critics name '{f.Title}' best picture", $"'{f.Title}' sweeps the {city} Film Critics awards", $"'{f.Title}' wins big with {city} critics, taking four prizes"),
                    $"{f.Star} won best actor, and the drama took four prizes in all.",
                [
                    $"'{f.Title}' was named best picture of the year by the {city} Film Critics Association on {f.Day}, one of four prizes for the drama.",
                    $"{f.Star} won best lead performance, and {f.Star2} took best director. The film also won for its screenplay.",
                    $"\"Critics groups don't always agree, but this year it wasn't close,\" said {f.Local}, the association's president.",
                    "## What it means for awards season",
                    "Critics prizes are an early signal in a long season. The film's studio is expected to campaign heavily in the coming months."
                ]),
                f => new Story("awards", "portrait", V(f, "Silver Lantern Awards red carpet: the best looks of the night", "Silver Lanterns red carpet: every look you need to see", "The boldest looks from the Silver Lantern Awards"),
                    "Bold color, old-Hollywood glamour and one unforgettable cape.",
                [
                    "The Silver Lantern Awards red carpet was a parade of bold color and old-Hollywood glamour, with a few surprises mixed in.",
                    $"{f.Star} stopped traffic in a sculpted emerald gown, while {f.Star2} went classic in a tailored black tuxedo with a velvet collar.",
                    $"\"It's a night to have fun with it,\" stylist {f.Local} said. \"The best looks were the ones where you could tell the person wearing it was having a good time.\"",
                    "## The cape everyone is talking about",
                    $"And then there was the cape: 12 feet of silver silk that trailed behind one nominee up the steps and took three assistants to manage."
                ]),
                f => new Story("awards", "portrait", V(f, $"{f.Star} will host this year's Silver Lantern Awards", $"{f.Star} tapped to host the Silver Lantern Awards", $"Silver Lantern Awards name {f.Star} as this year's host"),
                    "The comedian and actor promises a shorter show with more surprises.",
                [
                    $"{f.Star} will host this year's Silver Lantern Awards, the show's producers announced {f.Day}.",
                    $"It is the first hosting gig for {L(f.Star)}, who promised a show that is \"shorter, funnier and with at least one thing nobody saw coming.\"",
                    $"\"We wanted someone who makes the room feel lighter,\" said executive producer {f.Local}.",
                    "## When to watch",
                    $"The ceremony airs live on {f.Network} in February."
                ])
            ]
        };
    }

    /// <summary>What follows a story's own lines: background for organization stories, the next game for recaps, and so on.</summary>
    private IEnumerable<string> Filler(Site site, Item item)
    {
        var rng = new Random(item.Id ^ world.Seed);
        var f = item.Facts;
        var city = world.City;
        var next = item.Published.AddDays(rng.Next(2, 7)).ToString("dddd", Us);
        var league = Leagues[f.Sport];
        var rivals = league.Where(t => t != f.Team && t != f.Other).ToList();
        var tips = $"{site.Name} will continue to follow this story. Readers with information can reach the newsroom at tips@{SiteDomain(site)}.";
        switch (item.Kind)
        {
            case "org":
                yield return "## " + Pick(rng, ["The bigger picture", $"About {world.ShortName}", "Background"]);
                yield return $"{world.OrganizationName}, which employs about {Employees:N0} people, is one of the largest employers in the {city} area. The {OrgNoun} serves {Customers} across the region from its headquarters downtown.";
                yield return Pick(rng, [
                    $"Over the past two years the {OrgNoun} has taken on its {world.Projects[1].ToLowerInvariant()} and {world.Projects[2].ToLowerInvariant()} projects, according to presentations to its board.",
                    $"Its leadership team has turned over in recent years, and the {OrgNoun} has said it wants to be more open with the public about how it operates.",
                    $"{world.ShortName} works with outside vendors including {world.Vendors[0]} and {world.Vendors[1]} on parts of its operations, according to public records."]);
                yield return $"{Cap(Customers)} with questions can call {Phone}, weekdays from 8 a.m. to 5 p.m., or visit {world.Domain}.";
                break;
            case "nation" or "world":
                yield return $"{f.Local2.Split(' ')[0]} {L(f.Star)} contributed reporting.";
                break;
            case "civic" or "science" or "health" or "arts" or "sports":
                yield return "## " + item.Section switch { "Health" => "Resources", "Arts" => "If you go", "Sports" => "More coverage", _ => "Background" };
                yield return item.Section switch
                {
                    "Politics" => $"The {city} City Council meets on the first and third Tuesday of each month at City Hall. Meetings are open to the public and streamed on the city's website, where agendas and minutes are posted.",
                    "Business" => $"The {city} area's unemployment rate was 3.{world.Seed % 9 + 1} percent last month, according to state figures, below the national average.",
                    "Technology" => "Experts recommend keeping devices and software up to date, using a different strong password for each important account and turning on two-step verification wherever it is offered.",
                    "Science" => "Researchers and educators in the region say public interest in science has grown in recent years, with record turnout at museum programs, observatory nights and school field trips.",
                    "Health" => "The county health department posts updates on clinics, vaccines and public health programs on its website, and its nurse advice line is open 24 hours a day.",
                    "Arts" => "Tickets and schedules are available through the organizers' websites, and many events offer discounts for students and seniors.",
                    "Sports" => $"For scores, schedules and more coverage of the region's teams, visit the Sports section of {site.Name}.",
                    _ => $"{city} is home to about {40000 + world.Seed % 160000:N0} people and has grown steadily over the past decade, according to census figures, adding new housing on its east and west sides."
                };
                var more = item.Section switch
                {
                    "Politics" => "Council members serve four-year terms, with half of the seats on the ballot every two years.",
                    "Business" => $"The region's largest employers include {world.OrganizationName}, the {city} school district and {city} General Hospital.",
                    "Technology" => $"The {city} Public Library offers free one-on-one technology help on Tuesday evenings and Saturday mornings.",
                    "Science" => "The regional university's research budget has nearly doubled over the past decade, much of it from federal grants.",
                    "Health" => $"{city} General Hospital and three community clinics serve most of the county's residents.",
                    "Arts" => "The city's arts council awarded more than $1.2 million in grants last year to museums, theaters and individual artists.",
                    "Local" => "Neighborhood associations meet monthly, and many post their schedules on the city's website.",
                    _ => null
                };
                if (more != null)
                    yield return more;
                yield return tips;
                break;
            case "recap":
                yield return "## Key stats";
                yield return f.Sport switch
                {
                    "Football" or "College" => $"{Cap(The(f.Team))} outgained {The(f.Other)} {f.Number * 2 + 310} yards to {f.Number + 240} and did not turn the ball over. {Cap(The(f.Other))} converted {f.Number % 4 + 3} of {f.Number % 5 + 11} third downs.",
                    "Basketball" => $"{Cap(The(f.Team))} shot {44 + f.Number % 9} percent from the field and made {9 + f.Number % 8} 3-pointers. {Cap(The(f.Other))} committed {11 + f.Number % 9} turnovers, which led to {14 + f.Number % 12} points.",
                    "Baseball" => $"{Cap(Possessive(The(f.Team)))} starter worked {5 + f.Number % 3} innings, allowing {f.OtherScore} run{(f.OtherScore == 1 ? "" : "s")} on {f.OtherScore + 3} hits with {4 + f.Number % 6} strikeouts, and the bullpen did not allow a run.",
                    "Soccer" => $"{f.Team.Full} had {11 + f.Number % 9} shots, {Math.Max(f.Score, 5 + f.Number % 4)} on target, and {50 + f.Number % 16} percent of possession.",
                    _ => $"{Cap(The(f.Team))} outshot {The(f.Other)} {30 + f.Number % 10}-{20 + f.Number % 8 + f.OtherScore} and went 2-for-{4 + f.Number % 3} on the power play."
                };
                yield return "## Notes";
                yield return $"{(f.Other.Club ? f.Other.Full : $"The {f.Other.Short}")} were without {f.Star2}, who missed a second straight game with a {Injury(f with { Number = f.Number + 6 })} injury.";
                yield return $"Attendance was {f.Sport switch { "Football" => 61000 + f.Number * 90, "College" => 48000 + f.Number * 300, "Baseball" => 24000 + f.Number * 120, "Soccer" => 17000 + f.Number * 60, _ => 16200 + f.Number * 20 }:N0}.";
                yield return "## Up next";
                yield return $"{Cap(The(f.Team))} host {The(rivals[rng.Next(rivals.Count)])} on {next}. {Cap(The(f.Other))} visit {The(rivals[rng.Next(rivals.Count)])} the same night.";
                break;
            case "team":
                yield return "## What it means";
                yield return Q(f, 1,
                    $"{Cap(The(f.Team))} have won {f.Number % 3 + 3} of their last five and sit near the top of the standings.",
                    $"{Cap(The(f.Team))} have been one of the league's most consistent teams this season, and the front office has made clear it intends to contend.",
                    $"It has been an up-and-down season for {The(f.Team)}, who have alternated wins and losses for most of the past month.");
                yield return Q(f, 4,
                    "Season ticket renewals for next year are running ahead of last season's pace, the team said.",
                    "The front office has said it will look for help before the trade deadline if the right deal comes along.",
                    "Several players have said the locker room has grown closer during the recent stretch.");
                yield return "## Up next";
                yield return $"{Cap(The(f.Team))} play {The(rivals[rng.Next(rivals.Count)])} on {next}.";
                break;
            case "preview":
                yield return $"The game will be shown on {Pick(rng, Networks)} and streamed on the {site.Name} app.";
                break;
            case "individual":
                yield return "## What's next";
                yield return $"The tour moves on to the {Pick(rng, TeamCities.Where(c => c != f.Team.City).ToList())} {(item.Section == "Golf" ? "Invitational" : "Open")} next week, with most of the top players in the field.";
                yield return "Prize money and ranking points from this week's event will be final on Monday, when the updated standings are released.";
                break;
            case "industry":
                yield return "## The bigger picture";
                yield return Q(f, 2,
                    "Hollywood has spent the past two years recalibrating after a period of heavy spending on streaming, releasing fewer titles and betting more heavily on the ones it does make.",
                    "Studios and streamers are watching spending more closely than they were a few years ago, favoring proven creators, familiar stories and projects that can travel around the world.",
                    "The business has changed quickly. Theatrical windows are shorter, streamers are chasing profits instead of subscribers, and every big title is expected to do both.");
                yield return Q(f, 3,
                    "Industry analysts expect the trend to continue into next year, as companies weigh the cost of new projects against the steady returns of their libraries.",
                    "Executives have said they want fewer, bigger bets, and the projects that do get made are under more pressure than ever to find an audience quickly.",
                    "For audiences, the result has been fewer new releases but more attention on each one, from trailers to premieres to the opening weekend.");
                yield return $"Follow {site.Name} for the latest film, TV and streaming news.";
                break;
            case "celebrity":
                yield return "## The backstory";
                yield return $"{L(f.Star)} rose to fame with a breakout role in '{f.Title}' and has since become one of the most-searched names in entertainment.";
                yield return "Fans were quick to react online, and the news was among the most-discussed topics of the day.";
                yield return Pick(rng, ["This story has been updated.", $"{site.Name} has reached out for comment."]);
                break;
            case "music":
                yield return "## The backstory";
                yield return $"{f.Artist} broke through with a viral single five years ago and has since sold out theaters and arenas across the country, picking up a pair of Silver Lantern nominations along the way.";
                yield return "Tickets for upcoming shows are available through the artist's official website, and fan club members get access to a presale.";
                break;
            case "review":
                yield return item.Headline.Contains(f.Album)
                    ? $"'{f.Album}' is out now on all major streaming services and on vinyl."
                    : $"'{f.Title}' is rated PG-13 and runs 1 hour {40 + f.Number % 20} minutes. It opens in theaters on {item.Published.AddDays(2).ToString("dddd", Us)}.";
                break;
            case "awards":
                yield return "## The race so far";
                yield return "Awards season runs through the winter, with critics groups, guild prizes and festival awards building toward the Silver Lantern Awards in February.";
                break;
        }
    }

    private static readonly ProductType[] Catalog =
    [
        new("Women", ["Women's Scoop Neck Tee", "Women's Relaxed Fit Cotton T-Shirt", "Women's Slub Jersey Short Sleeve Top", "Women's Everyday Scoop Neck T-Shirt"],
            [("White", "shop-product9.png"), ("Red", "shop-product8.png")], null, 12, 26,
            "A soft, breathable cotton tee with a flattering scoop neck and a relaxed fit you can dress up or down.",
            ["Soft 100% combed ring-spun cotton jersey", "Flattering scoop neckline and short sleeves", "Relaxed fit through the body with a slightly longer hem", "Tag-free neck label for all-day comfort", "Pre-shrunk; machine wash cold, tumble dry low"],
            [("Fabric type", "100% Cotton"), ("Care instructions", "Machine Wash Cold"), ("Neck style", "Scoop Neck"), ("Sleeve type", "Short Sleeve"), ("Closure type", "Pull On"), ("Origin", "Imported")],
            ["XS", "S", "M", "L", "XL", "XXL"],
            [(5, "Perfect everyday tee", "Soft right out of the package and the scoop neck sits exactly where I want it. I bought a second color the same week."),
             (5, "True to size and washes well", "I usually wear a medium and the medium fits perfectly. Six washes in and no shrinking, no twisting seams."),
             (4, "Nice fabric, runs a little long", "Great quality for the price. It runs a bit long on me, but it looks good tucked in."),
             (5, "Bought one in every color", "Finally a basic tee that isn't see-through. These are now my weekend uniform."),
             (2, "Shrank after drying", "Loved it the first time, but after one hot dryer cycle it was noticeably shorter. Wash cold and hang dry.")]),
        new("Men", ["Men's Classic Crew Neck T-Shirt", "Men's Heavyweight Cotton Tee", "Men's Organic Cotton Pocket T-Shirt", "Men's Short Sleeve Jersey Tee", "Men's Garment-Dyed Crew Tee"],
            [("White", "shop-product2.png"), ("Red", "shop-product4.png")], null, 12, 28,
            "A heavyweight cotton crew neck that holds its shape wash after wash. The everyday tee, done right.",
            ["Midweight 100% combed ring-spun cotton, 5.3 oz", "Classic fit through the chest and waist", "Ribbed crew neck holds its shape", "Double-needle stitched sleeves and hem", "Pre-shrunk; machine wash cold, tumble dry low"],
            [("Fabric type", "100% Cotton"), ("Care instructions", "Machine Wash Cold"), ("Neck style", "Crew Neck"), ("Fit type", "Classic Fit"), ("Item weight", "5.3 Ounces"), ("Origin", "Imported")],
            ["S", "M", "L", "XL", "XXL", "3XL"],
            [(5, "My new go-to tee", "Soft, heavy cotton and it didn't shrink much after washing. I ordered three more."),
             (5, "Great fit", "I'm 6'0\" and 185 lbs and the large fits through the chest without being boxy. The collar stays flat."),
             (4, "Good shirt for the price", "Nice weight to the fabric. Runs a touch long, which I like because it stays tucked."),
             (5, "Still looks new", "Six months of weekly washing and there's no pilling or fading. Hard to beat."),
             (3, "Sleeves are a bit tight", "Quality is good but the sleeves are snug on my arms. Size up if you lift.")]),
        new("Kids", ["Kids' Crew Neck T-Shirt", "Boys' and Girls' Soft Cotton Tee", "Youth Short Sleeve Everyday Tee"],
            [("White", "shop-product2.png"), ("Red", "shop-product4.png")], null, 8, 16,
            "Durable, soft cotton tees built for recess, sports and everything in between.",
            ["Soft 100% cotton that stands up to everyday play", "Reinforced shoulder seams and double-stitched hem", "Tag-free comfort for sensitive skin", "Roomy fit with room to grow", "Machine washable; colors stay bright"],
            [("Fabric type", "100% Cotton"), ("Care instructions", "Machine Wash"), ("Neck style", "Crew Neck"), ("Age range", "4-16 Years"), ("Origin", "Imported")],
            ["XS (4-5)", "S (6-7)", "M (8)", "L (10-12)", "XL (14-16)"],
            [(5, "Survives my kids", "Two boys, one tee, a whole season of playground. Still in one piece and still soft."),
             (5, "Great for school uniforms", "Our school requires plain tees and these are the best we've found. Colors don't fade."),
             (4, "Runs slightly big", "My daughter is usually a small and this was roomy. Fine since she'll grow into it."),
             (3, "OK but wrinkles", "Nice and soft but comes out of the dryer pretty wrinkled.")]),
        new("Kitchen & Dining", ["Enamel Camp Mug, 12 oz", "Enamelware Coffee Mug with Stainless Rim, 16 oz", "Enamel Mug Set of 2, 12 oz", "Classic Enamel Tea Cup, 10 oz"],
            [("White", "shop-product1.png"), ("Red", "shop-product6.png")], "product3.png", 9, 24,
            "A lightweight, durable enamel mug with a stainless steel rim, equally at home at the breakfast table or the campsite.",
            ["Steel core with a glossy porcelain enamel finish", "Stainless steel rolled rim resists rust and chipping", "Lightweight and nearly unbreakable for travel and camping", "Comfortable C-handle stays cool to the touch", "Hand wash recommended; not for microwave use"],
            [("Material", "Enamel-Coated Steel"), ("Capacity", "12 Fluid Ounces"), ("Dishwasher safe", "No, hand wash recommended"), ("Microwave safe", "No"), ("Item weight", "6.4 Ounces")],
            null,
            [(5, "Perfect camp mug", "Took it on a week-long canoe trip. Light, tough and the handle stays cool enough to hold."),
             (5, "Charming and sturdy", "Looks great on the open shelf in our kitchen. Coffee stays warm longer than I expected."),
             (4, "Chips if you drop it", "Love the look, but I dropped one on tile and it chipped on the rim. Still usable."),
             (5, "Great gift", "Bought a set for my brother who camps every weekend. He uses them at home too.")]),
        new("Kitchen & Dining", ["Ceramic Coffee Mug, 15 oz", "Classic Diner Mug, 12 oz", "Stoneware Latte Mug, 14 oz"],
            [("White", "shop-product7.png")], null, 10, 22,
            "A heavy, glazed ceramic mug with a generous handle. Holds a real cup of coffee and keeps it hot.",
            ["High-fired ceramic with a lead-free glossy glaze", "Generous 15 oz capacity for a full cup with room for milk", "Thick walls keep drinks hot longer", "Dishwasher and microwave safe", "Wide, comfortable handle fits two fingers"],
            [("Material", "Ceramic"), ("Capacity", "15 Fluid Ounces"), ("Dishwasher safe", "Yes"), ("Microwave safe", "Yes"), ("Item weight", "14 Ounces")],
            null,
            [(5, "Holds a real cup of coffee", "Finally a mug that isn't tiny. Heavy, well made and the glaze is flawless."),
             (5, "Dishwasher safe and sturdy", "Six months of daily use in the dishwasher and it looks brand new."),
             (3, "Handle is small", "Great mug, but the handle is a little small for my hands."),
             (4, "Nice weight", "Feels substantial without being clunky. I'd buy a set of four.")]),
        new("Office & School", ["Spiral Notebook, College Ruled, 100 Sheets", "Top-Bound Spiral Sketch Pad, 9 x 12 in", "Wirebound Reporter's Notebook, 3-Pack", "Spiral Steno Pad, Gregg Ruled, 80 Sheets"],
            [("Red", "shop-product3.png")], null, 5, 15,
            "A sturdy spiral notebook with smooth, bright paper and a heavyweight cover that survives a backpack.",
            ["Thick 80 gsm paper resists ink bleed-through", "Heavyweight laminated cover protects your notes", "Double-wire spiral lies flat and folds back", "Micro-perforated pages tear out cleanly", "Acid-free paper for long-lasting notes"],
            [("Sheet count", "100"), ("Ruling", "College Ruled"), ("Paper weight", "80 GSM"), ("Sheet size", "8.5 x 11 Inches"), ("Binding", "Spiral")],
            null,
            [(5, "Paper is great for fountain pens", "No feathering and almost no bleed-through with my fountain pens. Rare at this price."),
             (5, "Sturdy cover", "Survived a semester in my backpack without the cover bending or the wire getting crushed."),
             (4, "Good notebook", "Nice paper and lies flat. Wish it came with a pocket folder."),
             (2, "Pages tear unevenly", "The perforations don't always tear clean. Fine for notes, not for handing in.")]),
        new("Bags & Totes", ["Heavy Canvas Tote Bag", "Organic Cotton Grocery Tote", "Everyday Market Tote with Gusset", "Zippered Canvas Shoulder Tote"],
            [("Natural", "shop-product5.png")], null, 12, 34,
            "A thick, natural cotton canvas tote with reinforced handles. Carries a full grocery run, a laptop or a weekend's worth of gear.",
            ["Heavy 12 oz natural cotton canvas", "Reinforced stitched handles carry up to 40 lbs", "Wide flat bottom gusset stands up on its own", "Inside pocket for keys and phone", "Machine washable; softens with every wash"],
            [("Material", "Cotton Canvas"), ("Dimensions", "15 x 16 x 5 Inches"), ("Handle drop", "11 Inches"), ("Closure", "Open Top"), ("Care instructions", "Machine Wash Cold")],
            null,
            [(5, "Carries a full grocery run", "I fit a full week of groceries in two of these. Handles haven't budged."),
             (5, "Thick canvas", "Much heavier than the free totes you get at events. Stands up on its own."),
             (3, "Wish it had a zipper", "Great bag, but things fall out when it tips over in the car."),
             (4, "Softens up nicely", "Stiff at first but after one wash it's perfect. Great for the beach.")]),
        new("Outdoor", ["Backpacking Enamel Mug, 10 oz", "Campfire Enamel Cup with Rolled Rim", "Trail Enamel Mug, Stackable, 12 oz"],
            [("Red", "shop-product6.png"), ("White", "shop-product1.png")], "product3.png", 9, 22,
            "Built for the trail: a nearly unbreakable enamel mug that goes from campfire to coffee in seconds.",
            ["Enamel-coated steel can be heated directly on a camp stove", "Rolled stainless rim resists chipping", "Stackable design saves space in your pack", "Weighs just 5 oz", "Easy to clean in the field"],
            [("Material", "Enamel-Coated Steel"), ("Capacity", "10 Fluid Ounces"), ("Stove safe", "Yes"), ("Item weight", "5 Ounces"), ("Color", "Assorted")],
            null,
            [(5, "Lives in my pack", "I heat water right in it on my stove. Indestructible so far."),
             (5, "Classic camp mug", "Exactly what I wanted. Looks great around the fire."),
             (4, "Gets hot", "Handle gets hot if you heat it directly over a flame. Use a glove.")]),
        new("Outdoor", ["Waxed Canvas Beach Tote", "Packable Trail Tote Bag"],
            [("Natural", "shop-product5.png")], null, 18, 36,
            "A rugged canvas tote for beach days, farmers markets and weekend trips.",
            ["Durable cotton canvas with a water-resistant finish", "Reinforced handles and base", "Flat-folds for packing", "Inner zip pocket for valuables"],
            [("Material", "Cotton Canvas"), ("Dimensions", "18 x 15 x 6 Inches"), ("Water resistant", "Yes"), ("Closure", "Open Top")],
            null,
            [(5, "Beach bag of the summer", "Sand shakes right out and it holds towels for the whole family."),
             (4, "Solid bag", "Good size and very sturdy. The finish smells a little at first."),
             (5, "Goes everywhere", "Market, beach, library. Holds up to all of it.")])
    ];

    private List<Item> CreateProducts(string siteName, int count, Func<int> nextId, Random rng)
    {
        var brand = $"{siteName} Basics";
        // every name and color of a department, shuffled, handed out round-robin so departments stay balanced
        var queues = ShoppingSections.ToDictionary(d => d, d => new Queue<(ProductType Type, string Name, (string Color, string Image) Variant)>());
        var round = new Dictionary<string, int>();
        var items = new List<Item>();
        while (items.Count < count)
        {
            foreach (var department in ShoppingSections)
            {
                if (items.Count >= count)
                    break;
                var queue = queues[department];
                if (queue.Count == 0)
                {
                    round[department] = round.GetValueOrDefault(department) + 1;
                    foreach (var combo in World.Shuffle(rng, Catalog.Where(t => t.Department == department).SelectMany(t => t.Names.SelectMany(n => t.Variants.Select(v => (t, n, v))))))
                        queue.Enqueue(combo);
                }

                var (type, name, variant) = queue.Dequeue();
                var pack = round[department] switch { 1 => "", 2 => ", 2-Pack", _ => $", {round[department] + 1}-Pack" };
                var title = $"{brand} {name}{(type.Variants.Length > 1 ? $", {variant.Color}" : "")}{pack}";
                var price = rng.Next(type.Low, type.High + 1) * (round[department] > 1 ? round[department] : 1) + (rng.Next(3) == 0 ? 0.49m : 0.99m);
                var listPrice = rng.Next(100) < 55 ? Math.Floor(price * (1 + rng.Next(12, 45) / 100m)) + 0.99m : price;
                var rating = Math.Round(3.7 + rng.NextDouble() * 1.2, 1);
                var ratings = rng.Next(100) < 20 ? rng.Next(8, 150) : rng.Next(150, 12000);
                var off = (int)Math.Round((1 - price / listPrice) * 100);
                var badge = off >= 25 ? "Limited time deal" : ratings > 4000 && rating >= 4.5 ? "Best Seller" : ratings < 150 ? "New Arrival" : rating >= 4.6 ? "Top Rated" : null;
                items.Add(new Item(nextId(), department, "product", title, type.Blurb, null,
                    world.ReferenceDate.AddDays(-rng.Next(20, 700)).AddHours(12), "/img/" + variant.Image, null, type.Features,
                    new Offer(type, variant.Color, "/img/" + variant.Image, price, listPrice, Math.Min(rating, 4.9), ratings, badge)));
            }
        }
        return items;
    }

    #endregion

    #region Pages

    private string Home(Site site) => site.Type switch
    {
        "shopping" => ShopHome(site),
        "sports" => SportsHome(site),
        "entertainment" => EntertainmentHome(site),
        _ => NewsHome(site)
    };

    private string SectionPage(Site site, string section) => site.Type switch
    {
        "shopping" => ShopSection(site, section),
        "sports" => SportsSection(site, section),
        "entertainment" => EntertainmentSection(site, section),
        _ => NewsSection(site, section)
    };

    // ---------- news ----------

    private string NewsHome(Site site)
    {
        var stories = site.Items.Where(i => i.Section != "Opinion").ToList();
        var lead = stories.FirstOrDefault(i => LargePhotos.Contains(i.Image)) ?? stories[0];
        var related = stories.Where(i => i != lead && i.Section == lead.Section).Take(2).ToList();
        var secondary = stories.Where(i => i != lead && !related.Contains(i)).Take(3).ToList();
        var shown = related.Concat(secondary).Append(lead).ToHashSet();

        var html = new StringBuilder();
        var newest = stories[0];
        html.Append($"<div class=\"n-alert n-ui\"><div class=\"wrap\"><b>Live</b><a href=\"/article/{newest.Id}\">{E(newest.Headline)}</a><span>Updated {Ago(newest.Published)}</span></div></div>");
        html.Append("<div class=\"wrap n-top\"><div class=\"n-pkg\">");
        html.Append($"<div class=\"n-lead\"><a class=\"ratio\" href=\"/article/{lead.Id}\">{Img(lead)}</a><div class=\"n-kicker\">{E(lead.Section)}</div>");
        html.Append($"<h2><a href=\"/article/{lead.Id}\">{E(lead.Headline)}</a></h2><p class=\"n-dek\">{E(lead.Summary)}</p><ul class=\"n-bul\">");
        foreach (var item in related)
            html.Append($"<li><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></li>");
        html.Append($"</ul><div class=\"n-meta\">{ReadTime(site, lead)} min read</div></div><div class=\"n-sec\">");
        foreach (var (item, n) in secondary.Select((x, n) => (x, n)))
        {
            html.Append("<article class=\"n-story\">");
            if (n == 0)
                html.Append($"<a class=\"ratio\" href=\"/article/{item.Id}\">{Img(item)}</a>");
            html.Append($"<div class=\"n-kicker\">{E(item.Section)}</div><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3><p>{E(item.Summary)}</p><div class=\"n-meta\">{Ago(item.Published)}</div></article>");
        }
        html.Append("</div></div>");
        html.Append(NewsRail(site, true));
        html.Append("</div><div class=\"wrap n-blocks\">");

        var blocks = site.Sections.Where(s => s != "Opinion")
            .Select(section => (Section: section, Items: stories.Where(i => i.Section == section && !shown.Contains(i)).Take(4).ToList()))
            .Where(b => b.Items.Count > 0).ToList();
        var lastHalf = blocks.Count(b => b.Items.Count <= 2) % 2 == 1 ? blocks.Last(b => b.Items.Count <= 2).Section : null;
        foreach (var (section, items) in blocks)
        {
            html.Append($"<section class=\"n-block{(items.Count <= 2 && section != lastHalf ? " half" : "")}\"><h2 class=\"n-ui\"><a href=\"/{Slug(section)}\">{E(section)}</a></h2><div class=\"n-g4 n-c{items.Count}\">");
            foreach (var item in items)
                html.Append($"<article class=\"n-story\"><a class=\"ratio\" href=\"/article/{item.Id}\">{Img(item)}</a><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3><p>{E(item.Summary)}</p></article>");
            html.Append("</div></section>");
        }
        html.Append("</div>");
        return Page(site, site.Tagline, html.ToString());
    }

    private string NewsRail(Site site, bool latest)
    {
        var html = new StringBuilder("<aside class=\"n-rail\">");
        if (latest)
        {
            html.Append("<section><h2>Latest</h2><ul class=\"n-latest\">");
            foreach (var item in site.Items.Take(7))
                html.Append($"<li><time>{item.Published.ToString("h:mm tt", Us)}</time><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></li>");
            html.Append("</ul></section>");
        }
        html.Append("<section><h2>Opinion</h2><div class=\"n-op\">");
        foreach (var item in site.Items.Where(i => i.Section == "Opinion").Take(3))
            html.Append($"<a href=\"/article/{item.Id}\"><span><span class=\"who\">{E(item.Author.Name)}</span><h3>{E(item.Headline)}</h3></span>{Avatar(item.Author)}</a>");
        html.Append("</div></section>");
        html.Append(MostRead(site, "n-mr", "Most Read"));
        return html.Append("</aside>").ToString();
    }

    private static string MostRead(Site site, string cls, string title)
    {
        var html = new StringBuilder($"<section><h2>{title}</h2><ol class=\"{cls}\">");
        foreach (var item in site.Items.OrderBy(i => i.Id * 7919 % 1009).Take(5))
            html.Append($"<li><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></li>");
        return html.Append("</ol></section>").ToString();
    }

    private string NewsSection(Site site, string section)
    {
        var items = site.Items.Where(i => i.Section == section).ToList();
        var top = items.FirstOrDefault(i => LargePhotos.Contains(i.Image)) ?? items[0];
        var river = items.Where(i => i != top).Concat(site.Items.Where(i => i.Section != section && i.Section != "Opinion")).Take(10).ToList();
        var html = new StringBuilder();
        html.Append($"<div class=\"wrap\"><header class=\"n-sechead\"><h1>{E(section)}</h1><p class=\"n-ui\">{E(SectionBlurb(section))}</p></header><div class=\"n-top\"><div>");
        html.Append($"<article class=\"n-sectop\"><a class=\"ratio\" href=\"/article/{top.Id}\">{Img(top)}</a><div><h2><a href=\"/article/{top.Id}\">{E(top.Headline)}</a></h2><p class=\"n-dek\">{E(top.Summary)}</p><div class=\"n-meta\">By {E(top.Author.Name)} · {Ago(top.Published)}</div></div></article>");
        html.Append("<h2 class=\"n-riverh n-ui\">Latest</h2>");
        foreach (var item in river)
            html.Append(RiverRow(item));
        html.Append("</div>");
        html.Append(NewsRail(site, false));
        html.Append("</div></div>");
        return Page(site, section, html.ToString(), section);
    }

    private string SectionBlurb(string section) => section switch
    {
        "Local" => $"News from {world.City} and the surrounding region",
        "Nation" => "National news, policy and the economy",
        "World" => "International news and analysis",
        "Politics" => "City hall, the county board and the statehouse",
        "Business" => "Companies, jobs, markets and the regional economy",
        "Technology" => "The tech that runs our lives and our institutions",
        "Science" => "Research, discovery and the natural world",
        "Health" => "Health care, wellness and medical research",
        "Sports" => "Scores, stories and analysis from the region's teams",
        "Arts" => "Film, music, theater and the visual arts",
        "Opinion" => "Columns, editorials and guest essays",
        _ => ""
    };

    private string RiverRow(Item item) =>
        $"<article class=\"river\"><time>{item.Published.ToString("MMM d", Us)}</time><div><div class=\"r-kicker\">{E(item.Section)}</div><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3>" +
        $"<p>{E(item.Summary)}</p><div class=\"r-meta\">By {E(item.Author.Name)}</div></div><a class=\"ratio r-img\" href=\"/article/{item.Id}\">{Media(item)}</a></article>";

    // ---------- sports ----------

    private string SportsHome(Site site)
    {
        var lead = site.Items.FirstOrDefault(i => LargePhotos.Contains(i.Image)) ?? site.Items[0];
        var feed = site.Items.Where(i => i != lead).ToList();
        var html = new StringBuilder("<div class=\"s-main\">");
        html.Append("<aside class=\"s-left\"><div class=\"s-card\"><h2 class=\"s-ch\">Top Headlines</h2><ul class=\"s-heads\">");
        foreach (var item in feed.Take(9))
            html.Append($"<li><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></li>");
        html.Append("</ul></div><div class=\"s-card\"><h2 class=\"s-ch\">Sports</h2><ul class=\"s-heads s-links\">");
        foreach (var section in site.Sections)
            html.Append($"<li><a href=\"/{Slug(section)}\">{E(section)}</a></li>");
        html.Append("</ul></div></aside><div class=\"s-center\">");
        html.Append($"<article class=\"s-card s-lead\"><a href=\"/article/{lead.Id}\">{Media(lead)}</a><div class=\"s-lead-text\"><span class=\"s-label\">{E(lead.Section)}</span><h1><a href=\"/article/{lead.Id}\">{E(lead.Headline)}</a></h1><p>{E(lead.Summary)}</p><div class=\"s-meta\">{Ago(lead.Published)} · {E(lead.Author.Name)}</div></div></article>");
        foreach (var item in feed.Skip(9).Concat(feed.Take(9)).Take(14))
            html.Append(SportsCard(item));
        html.Append("</div><aside class=\"s-right\">");
        html.Append(Standings("Football", "Football Power Rankings", 6));
        html.Append("<div class=\"s-card\">").Append(MostRead(site, "s-mr", "Most Popular").Replace("<h2>", "<h2 class=\"s-ch\">")).Append("</div>");
        html.Append(Standings("Basketball", "Basketball Standings", 6));
        html.Append("</aside></div>");
        return Page(site, site.Tagline, html.ToString());
    }

    private string SportsCard(Item item)
    {
        var color = item.Image is "match" or "team" || item.Kind is "recap" or "team" or "preview" ? item.Facts.Team.Primary : "#121213";
        return $"<article class=\"s-card s-story\" style=\"border-top-color:{color}\"><a class=\"s-thumb\" href=\"/article/{item.Id}\">{Media(item)}</a><div>" +
               $"<span class=\"s-label\">{E(item.Section)}</span><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3><p>{E(item.Summary)}</p>" +
               $"<div class=\"s-meta\">{Ago(item.Published)} · {E(item.Author.Name)}</div></div></article>";
    }

    private string SportsSection(Site site, string section)
    {
        var items = site.Items.Where(i => i.Section == section).ToList();
        var games = site.Games.Where(g => g.Sport == section).ToList();
        var html = new StringBuilder($"<div class=\"s-sechead\"><div class=\"wrap\"><h1>{E(section)}</h1><nav><a href=\"#news\">News</a>");
        if (games.Count > 0)
            html.Append("<a href=\"#scores\">Scores</a><a href=\"#standings\">Standings</a>");
        html.Append("</nav></div></div><div class=\"s-main s-two\"><div class=\"s-center\">");
        if (games.Count > 0)
        {
            html.Append("<div class=\"s-card\" id=\"scores\"><h2 class=\"s-ch\">Scoreboard</h2><div class=\"s-board\">");
            foreach (var game in games)
                html.Append(GameBox(game, "s-bgame"));
            html.Append("</div></div>");
        }
        html.Append("<div id=\"news\"></div>");
        foreach (var item in items)
            html.Append(SportsCard(item));
        foreach (var item in site.Items.Where(i => i.Section != section).Take(Math.Max(0, 6 - items.Count)))
            html.Append(SportsCard(item));
        html.Append("</div><aside class=\"s-right\">");
        if (Leagues.ContainsKey(section))
            html.Append(Standings(section, $"{section} Standings", 10, "standings"));
        html.Append("<div class=\"s-card\">").Append(MostRead(site, "s-mr", "Most Popular").Replace("<h2>", "<h2 class=\"s-ch\">")).Append("</div>");
        html.Append("</aside></div>");
        return Page(site, section, html.ToString(), section);
    }

    private string Standings(string sport, string title, int rows, string id = null)
    {
        var rng = new Random(world.Seed ^ StableHash(sport));
        var soccer = sport == "Soccer";
        var played = rng.Next(10, 17);
        var table = Leagues[sport].Select(t =>
        {
            var games = played - rng.Next(0, 2);
            var w = rng.Next(2, games - 1);
            var d = soccer ? rng.Next(0, games - w) : 0;
            return (Team: t, W: w, L: games - w - d, D: d, Streak: $"{(rng.Next(2) == 0 ? "W" : "L")}{rng.Next(1, 5)}");
        }).OrderByDescending(r => soccer ? r.W * 3 + r.D : (double)r.W / (r.W + r.L)).ThenBy(r => r.Team.City).Take(rows).ToList();

        var html = new StringBuilder($"<div class=\"s-card\"{(id == null ? "" : $" id=\"{id}\"")}><h2 class=\"s-ch\">{E(title)}</h2><table class=\"s-table\"><thead><tr><th>Team</th>");
        html.Append(soccer ? "<th>W</th><th>D</th><th>L</th><th>PTS</th>" : "<th>W</th><th>L</th><th>PCT</th><th>STRK</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var (row, n) in table.Select((r, n) => (r, n + 1)))
        {
            html.Append($"<tr><td><span class=\"s-tm\"><span class=\"s-rk\">{n}</span>{Crest(row.Team)}{E(row.Team.Short)}</span></td>");
            html.Append(soccer
                ? $"<td>{row.W}</td><td>{row.D}</td><td>{row.L}</td><td><b>{row.W * 3 + row.D}</b></td>"
                : $"<td>{row.W}</td><td>{row.L}</td><td>{(double)row.W / (row.W + row.L):.000}</td><td>{row.Streak}</td>");
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

    private static string GameBox(Game game, string cls)
    {
        var final = game.Status == "Final";
        var body = $"<div class=\"s-g-top\"><span>{E(game.Sport)}</span><span>{E(game.Status)}</span></div>" +
                   $"<div class=\"s-g-row{(final ? " win" : "")}\">{Crest(game.Winner)}<span>{game.Winner.Abbr}</span><b>{(final ? game.WinnerScore.ToString() : "")}</b></div>" +
                   $"<div class=\"s-g-row\">{Crest(game.Loser)}<span>{game.Loser.Abbr}</span><b>{(final ? game.LoserScore.ToString() : "")}</b></div>";
        return game.Recap == null
            ? $"<div class=\"{cls}\">{body}</div>"
            : $"<a class=\"{cls}\" href=\"/article/{game.Recap.Id}\">{body}<span class=\"s-g-recap\">Recap</span></a>";
    }

    // the scoring plays that add up to a final score: football in touchdowns, field goals and safeties, the rest one at a time
    private static IEnumerable<int> Plays(string sport, int total)
    {
        if (sport is not ("Football" or "College"))
        {
            for (var left = total; left > 0; left--)
                yield return 1;
            yield break;
        }
        var touchdowns = total / 7;
        int[] rest = (total % 7) switch { 0 => [], 1 => [8], 2 => [2], 3 => [3], 4 => [8, 3], 5 => [3, 2], _ => [3, 3] };
        if (total % 7 is 1 or 4 && touchdowns > 0)
            touchdowns--;
        else if (total % 7 is 1 or 4)
            rest = [2, 2];
        for (var n = 0; n < touchdowns; n++)
            yield return 7;
        foreach (var play in rest)
            yield return play;
    }

    private static string LineScore(Item item)
    {
        var f = item.Facts;
        var rng = new Random(item.Id);
        var periods = f.Sport switch { "Baseball" => 9, "Hockey" => 3, "Soccer" => 2, _ => 4 };
        var html = new StringBuilder("<div class=\"s-box\"><table class=\"s-table\"><thead><tr><th>Final</th>");
        for (var p = 1; p <= periods; p++)
            html.Append($"<th>{p}</th>");
        html.Append(f.Sport == "Baseball" ? "<th>R</th><th>H</th><th>E</th>" : "<th>T</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var (team, total) in new[] { (f.Team, f.Score), (f.Other, f.OtherScore) })
        {
            var parts = new int[periods];
            if (f.Sport == "Basketball")
            {
                // quarters stay close to a quarter of the total, as they do in real games
                for (var p = 0; p < periods - 1; p++)
                    parts[p] = total / periods + rng.Next(-6, 7);
                parts[^1] = total - parts.Sum();
            }
            else
                foreach (var play in Plays(f.Sport, total))
                    parts[rng.Next(periods)] += play;
            html.Append($"<tr><td><span class=\"s-tm\">{Crest(team)}{E(team.Short)}</span></td>");
            foreach (var part in parts)
                html.Append($"<td>{part}</td>");
            html.Append(f.Sport == "Baseball" ? $"<td><b>{total}</b></td><td>{total + rng.Next(2, 6)}</td><td>{rng.Next(0, 3)}</td>" : $"<td><b>{total}</b></td>");
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }

    // ---------- entertainment ----------

    private string EntertainmentHome(Site site)
    {
        var photosUsed = new HashSet<string>();
        var hero = Fresh(site.Items.Where(i => LargePhotos.Contains(i.Image)).Take(1).Concat(site.Items), photosUsed, 5);
        var river = Fresh(site.Items.Except(hero), photosUsed, 10);
        var rest = site.Items.Except(hero).Except(river).ToList();
        var html = new StringBuilder("<div class=\"wrap\">");
        html.Append(EntertainmentHero(hero));
        html.Append("<div class=\"e-cols\"><div><h2 class=\"e-h\">Latest News</h2><div class=\"e-river\">");
        foreach (var item in river)
            html.Append(EntertainmentRow(item));
        html.Append("</div></div>");
        html.Append(EntertainmentRail(site));
        html.Append("</div>");
        var photos = Fresh(rest.Where(i => i.Section is "Celebrity" or "Awards" or "Film" or "TV"), photosUsed, 4);
        html.Append("<section class=\"e-sec\"><h2 class=\"e-h\">Photos</h2><div class=\"e-photos\">");
        foreach (var item in photos)
            html.Append($"<a href=\"/article/{item.Id}\">{Img(item)}<span class=\"e-ov\"><span class=\"e-cam\">{Icon("camera")} Photos</span><h3>{E(item.Headline)}</h3></span></a>");
        html.Append("</div></section>");
        foreach (var section in new[] { "TV", "Music", "Film" })
        {
            var items = Fresh(rest.Except(photos).Where(i => i.Section == section).Concat(site.Items.Where(i => i.Section == section)), photosUsed, 3);
            html.Append($"<section class=\"e-sec\"><h2 class=\"e-h\"><a href=\"/{Slug(section)}\">{E(section)}</a></h2><div class=\"e-g3\">");
            foreach (var item in items)
                html.Append($"<article><a class=\"ratio r169\" href=\"/article/{item.Id}\">{Img(item)}</a><span class=\"e-cat\">{E(item.Section)}</span><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3></article>");
            html.Append("</div></section>");
        }
        html.Append("</div>");
        return Page(site, site.Tagline, html.ToString());
    }

    // up to n distinct items, those with a photo not yet on the page first
    private static List<Item> Fresh(IEnumerable<Item> candidates, HashSet<string> used, int n)
    {
        var pool = candidates.Distinct().ToList();
        var picked = pool.Where(i => !used.Contains(i.Image)).GroupBy(i => i.Image).Select(g => g.First()).Take(n).ToList();
        picked.AddRange(pool.Except(picked).Where(i => picked.All(p => p.Image != i.Image)).GroupBy(i => i.Image).Select(g => g.First()).Take(n - picked.Count));
        picked.AddRange(pool.Except(picked).Take(n - picked.Count));
        used.UnionWith(picked.Select(i => i.Image));
        return pool.Where(picked.Contains).ToList();
    }

    private static string EntertainmentHero(List<Item> items)
    {
        var html = new StringBuilder("<div class=\"e-hero\">");
        foreach (var item in items)
            html.Append($"<a href=\"/article/{item.Id}\">{Img(item)}<span class=\"e-ov\"><span class=\"e-cat\">{E(item.Section)}</span><h2>{E(item.Headline)}</h2></span></a>");
        return html.Append("</div>").ToString();
    }

    private string EntertainmentRow(Item item) =>
        $"<article><a class=\"ratio r169\" href=\"/article/{item.Id}\">{Img(item)}</a><div><span class=\"e-cat\">{E(item.Section)}</span><h3><a href=\"/article/{item.Id}\">{E(item.Headline)}</a></h3>" +
        $"<p>{E(item.Summary)}</p><div class=\"e-meta\">By {E(item.Author.Name)} · {Ago(item.Published)}</div></div></article>";

    private static string EntertainmentRail(Site site)
    {
        var html = new StringBuilder("<aside class=\"e-rail\"><section><h2 class=\"e-h\">Most Popular</h2><ol class=\"e-pop\">");
        foreach (var item in site.Items.OrderBy(i => i.Id * 7919 % 1009).Take(5))
            html.Append($"<li><a href=\"/article/{item.Id}\">{E(item.Headline)}</a><a href=\"/article/{item.Id}\">{Img(item)}</a></li>");
        html.Append($"</ol></section><section class=\"e-nl\"><h2>Get the {E(site.Name)} newsletter</h2><p>The day's biggest stories, delivered every morning.</p><div class=\"e-nl-f\"><input type=\"email\" placeholder=\"Email address\" aria-label=\"Email address\"><button type=\"button\">Sign up</button></div></section>");
        html.Append("<section><h2 class=\"e-h\">Reviews</h2>");
        foreach (var item in site.Items.Where(i => i.Kind == "review").Take(3))
            html.Append($"<a class=\"e-rev\" href=\"/article/{item.Id}\"><b>{E(item.Lines[^1][^2..].Trim())}</b><span>{E(item.Headline)}</span></a>");
        return html.Append("</section></aside>").ToString();
    }

    private string EntertainmentSection(Site site, string section)
    {
        var items = site.Items.Where(i => i.Section == section).ToList();
        var more = site.Items.Where(i => i.Section != section).Take(Math.Max(0, 8 - items.Count));
        var html = new StringBuilder($"<div class=\"wrap\"><header class=\"e-sechead\"><h1>{E(section)}</h1></header>");
        html.Append(EntertainmentHero(items.Take(3).ToList()).Replace("e-hero", "e-hero e-hero3"));
        html.Append("<div class=\"e-cols\"><div><h2 class=\"e-h\">More " + E(section) + "</h2><div class=\"e-river\">");
        foreach (var item in items.Skip(3).Concat(more))
            html.Append(EntertainmentRow(item));
        html.Append("</div></div>").Append(EntertainmentRail(site)).Append("</div></div>");
        return Page(site, section, html.ToString(), section);
    }

    // ---------- articles and authors ----------

    private string Article(Site site, Item item)
    {
        var body = item.Lines.Concat(Filler(site, item)).ToList();
        var related = site.Items.Where(i => i.Section == item.Section && i != item).Concat(site.Items.Where(i => i.Section != item.Section)).Take(3).ToList();
        var opinion = item.Section == "Opinion";
        var html = new StringBuilder($"<div class=\"wrap a-wrap a-{site.Type}\"><article class=\"a-main\">");
        html.Append($"<div class=\"a-kicker\"><a href=\"/{Slug(item.Section)}\">{E(item.Section)}</a>{(item.Kind == "review" ? "<span>Review</span>" : "")}</div>");
        html.Append($"<h1>{E(item.Headline)}</h1><p class=\"a-dek\">{E(item.Summary)}</p>");
        html.Append($"<div class=\"a-by\">{Avatar(item.Author)}<div><div>By <a href=\"/author/{item.Author.Slug}\">{E(item.Author.Name)}</a>{(opinion ? ", Opinion Columnist" : site.Type == "news" ? "" : $", {E(site.Name)} Staff")}</div>");
        html.Append($"<time>Published {ApDate(item.Published)}, {item.Published.ToString("h:mm tt", Us)} ET</time></div><div class=\"a-share\" aria-label=\"Share\">{Icon("share")}{Icon("mail")}{Icon("bookmark")}</div></div>");
        if (!opinion)
            html.Append($"<figure class=\"a-fig\"><div class=\"ratio{(site.Type == "news" ? "" : " r169")}\">{Media(item)}</div><figcaption>{E(Caption(site, item))} <span>{E(Credit(site, item))}</span></figcaption></figure>");
        if (item.Kind == "recap" && site.Type == "sports")
            html.Append(LineScore(item));
        html.Append("<div class=\"a-body\">");
        for (var n = 0; n < body.Count; n++)
        {
            var line = body[n];
            html.Append(line.StartsWith("## ") ? $"<h2>{E(line[3..])}</h2>" : $"<p>{E(line)}</p>");
            if (n == 2 && related.Count > 0 && site.Type != "news")
                html.Append($"<p class=\"a-rel\"><b>Related:</b> <a href=\"/article/{related[0].Id}\">{E(related[0].Headline)}</a></p>");
        }
        html.Append("</div>");
        html.Append($"<div class=\"a-bio\">{Avatar(item.Author)}<p><a href=\"/author/{item.Author.Slug}\">{E(item.Author.Name)}</a> {E(Bio(site, item.Author))}</p></div>");
        html.Append($"<section class=\"a-more\"><h2>More from {E(site.Name)}</h2><div class=\"a-g3\">");
        foreach (var r in related)
            html.Append($"<article><a class=\"ratio r169\" href=\"/article/{r.Id}\">{Media(r)}</a><div><div class=\"a-mk\">{E(r.Section)}</div><h3><a href=\"/article/{r.Id}\">{E(r.Headline)}</a></h3></div></article>");
        html.Append("</div></section></article>");
        html.Append(ArticleRail(site, item));
        html.Append("</div>");
        return Page(site, item.Headline, html.ToString(), item.Section);
    }

    private string ArticleRail(Site site, Item item) => site.Type switch
    {
        "news" => NewsRail(site, true),
        "entertainment" => EntertainmentRail(site),
        _ => "<aside class=\"s-right\"><div class=\"s-card\">" + MostRead(site, "s-mr", "Most Popular").Replace("<h2>", "<h2 class=\"s-ch\">") + "</div>" +
             (Leagues.ContainsKey(item.Section) ? Standings(item.Section, $"{item.Section} Standings", 8) : "") + "</aside>"
    };

    private string AuthorPage(Site site, Writer writer)
    {
        var items = site.Items.Where(i => i.Author == writer).ToList();
        var heading = $"Latest from {writer.Name.Split(' ')[0]}";
        if (items.Count == 0)
            (items, heading) = (site.Items.Where(i => i.Section == writer.Beat).ToList(), $"Latest in {writer.Beat}");
        var html = new StringBuilder($"<div class=\"wrap au a-{site.Type}\"><header class=\"au-head\">{Avatar(writer, "au-ava")}<div><h1>{E(writer.Name)}</h1>");
        html.Append($"<p class=\"au-role\">{(writer.Beat == "Opinion" ? "Opinion Columnist" : $"Staff Writer, {E(writer.Beat)}")}</p><p>{E(writer.Name)} {E(Bio(site, writer))}</p>");
        html.Append($"<p class=\"au-mail\">{Icon("mail")} {E(writer.Name.ToLowerInvariant().Replace(' ', '.'))}@{E(SiteDomain(site))}</p></div></header>");
        html.Append($"<h2 class=\"au-h\">{E(heading)}</h2>");
        foreach (var item in items)
            html.Append(RiverRow(item));
        html.Append("</div>");
        return Page(site, writer.Name, html.ToString());
    }

    private string Bio(Site site, Writer writer)
    {
        var rng = new Random(StableHash(writer.Name) ^ world.Seed);
        var since = world.ReferenceDate.Year - rng.Next(2, 15);
        var where = Pick(rng, ["Columbus", "Sacramento", "Milwaukee", "Tampa", "Albany", "Tucson", "Omaha", "Raleigh"]);
        return writer.Beat == "Opinion"
            ? $"is an opinion columnist for {site.Name}, writing about {world.City}, its neighborhoods and the people who run them. Before joining the opinion section in {since}, {writer.Name.Split(' ')[0]} spent a decade as a reporter in {where}."
            : $"covers {Beat(writer.Beat)} for {site.Name} and has been with the newsroom since {since}. Previously, {writer.Name.Split(' ')[0]} reported for newspapers in {where} and {Pick(rng, ["Boise", "Charleston", "Des Moines", "Spokane", "Lansing"])}.";
    }

    private static string Beat(string section) => section switch
    {
        "Local" => "local news", "Nation" => "national news", "World" => "international news", "Arts" => "the arts", "TV" => "television",
        "Celebrity" => "celebrity news", "Box Office" => "the box office", "Reviews" => "film and music criticism", "Awards" => "awards season",
        "College" => "college sports", _ => section.ToLowerInvariant()
    };

    // only captions that cannot contradict the photo; everything else gets a credit alone
    private static string Caption(Site site, Item item)
    {
        var f = item.Facts;
        if (!item.Image.StartsWith('/'))
            return item.Kind == "recap" ? $"{f.Team.Full} vs. {f.Other.Full}, {f.Day}." : $"{f.Team.Full}.";
        var file = item.Image[5..];
        return site.Type switch
        {
            "entertainment" when Photos["portrait"].Contains(file) && item.Kind is "celebrity" or "awards" => $"{f.Star} at an event earlier this year.",
            "entertainment" when Photos["portrait"].Contains(file) && item.Section is "Film" or "TV" or "Reviews" => $"{f.Star} in a scene from '{f.Title}'.",
            "entertainment" when item.Section == "Music" && !Photos["records"].Contains(file) => $"{f.Artist} on stage earlier this year.",
            "sports" when Photos["fans"].Contains(file) => $"Fans at {Venue(f.Team)} earlier this season.",
            _ => ""
        };
    }

    private static string Credit(Site site, Item item)
    {
        var rng = new Random(item.Id);
        return site.Type switch
        {
            "entertainment" when item.Kind is "industry" or "review" && Photos["portrait"].Contains(item.Image[5..]) => $"Courtesy of {item.Facts.Studio}",
            "entertainment" => $"{Pick(rng, LocalFirst)} {Pick(rng, LocalLast)}/{site.Name}",
            "sports" when item.Image is "match" or "team" => $"{site.Name} illustration",
            _ => $"{Pick(rng, LocalFirst)} {Pick(rng, LocalLast)} for {site.Name}"
        };
    }

    private int ReadTime(Site site, Item item) => Math.Max(3, item.Lines.Concat(Filler(site, item)).Sum(l => l.Split(' ').Length) / 200 + 2);

    // ---------- shopping ----------

    private string ShopHome(Site site)
    {
        // the deepest discount of each department first, then the next of each, so one department cannot fill the row
        var deals = site.Items.Where(i => i.Offer.ListPrice > i.Offer.Price).GroupBy(i => i.Section)
            .SelectMany(g => g.OrderByDescending(i => 1 - i.Offer.Price / i.Offer.ListPrice).Select((item, rank) => (item, rank)))
            .OrderBy(x => x.rank).ThenByDescending(x => 1 - x.item.Offer.Price / x.item.Offer.ListPrice).Select(x => x.item).Take(10).ToList();
        var html = new StringBuilder("<div class=\"h-page\">");
        var saleEnds = world.ReferenceDate.AddDays(7 - (int)world.ReferenceDate.DayOfWeek).ToString("dddd, MMMM d", Us);
        var biggest = site.Items.Max(i => (int)Math.Round((1 - i.Offer.Price / i.Offer.ListPrice) * 100));
        html.Append($"<section class=\"h-hero\"><div class=\"h-hero-text\"><span class=\"h-tag\">Limited time</span><h1>{(biggest >= 10 ? $"Up to {biggest}% off" : "New")} new-season basics</h1><p>Soft tees, sturdy totes and the mugs everyone is gifting this year. Sale ends {saleEnds}.</p><a class=\"h-btn\" href=\"/{Slug("Women")}\">Shop the sale</a></div><div class=\"h-hero-img\"><img src=\"/img/post-header1.jpg\" alt=\"Woman wearing a cotton tee\"></div></section>");

        var mugsFrom = site.Items.Where(i => i.Section is "Kitchen & Dining" or "Outdoor" && i.Headline.Contains("Mug")).Select(i => i.Offer.Price).DefaultIfEmpty(9.99m).Min();
        html.Append("<div class=\"h-tiles\">");
        foreach (var (title, departments, link, photo) in new[]
                 {
                     ("Tees for everyone", new[] { "Women", "Men", "Kids" }, "Shop all tees", null),
                     ($"Mugs from {mugsFrom.ToString("C", Us)}", new[] { "Kitchen & Dining", "Outdoor" }, "Shop drinkware", null),
                     ("Back to the desk", new[] { "Office & School" }, "Shop office supplies", "/img/post3.jpg"),
                     ("Weekend ready", new[] { "Outdoor", "Bags & Totes" }, "Shop outdoor gear", "/img/last-phot12-large.jpg")
                 })
        {
            html.Append($"<div class=\"h-tile\"><h2>{E(title)}</h2>");
            if (photo != null)
                html.Append($"<a class=\"h-solo\" href=\"/{Slug(departments[0])}\"><img src=\"{photo}\" alt=\"{E(title)}\"></a>");
            else
            {
                // one of each product photo first, then repeats to fill the four squares
                var inDepartments = site.Items.Where(i => departments.Contains(i.Section) && (photo != null || !title.StartsWith("Mugs") || i.Headline.Contains("Mug") || i.Headline.Contains("Cup"))).ToList();
                var picks = inDepartments.GroupBy(i => i.Image).Select(g => g.First()).Concat(inDepartments).Distinct().Take(4);
                html.Append("<div class=\"h-quad\">");
                foreach (var item in picks)
                    html.Append($"<a href=\"/product/{item.Id}\"><img src=\"{item.Image}\" alt=\"{E(item.Headline)}\"><span>{E(ShortName(item))}</span></a>");
                html.Append("</div>");
            }
            html.Append($"<a class=\"h-more\" href=\"/{Slug(departments[0])}\">{E(link)}</a></div>");
        }
        html.Append("</div>");

        html.Append($"<section class=\"h-sec\" id=\"deals\"><h2>Today's Deals <a href=\"/{Slug("Kitchen & Dining")}\">See all deals</a></h2><div class=\"h-strip\">");
        foreach (var item in deals)
            html.Append(ProductCard(item));
        html.Append("</div></section>");

        foreach (var department in new[] { "Kitchen & Dining", "Women", "Men" })
        {
            html.Append($"<section class=\"h-sec\"><h2>Best Sellers in {E(department)} <a href=\"/{Slug(department)}\">Shop now</a></h2><div class=\"h-strip\">");
            foreach (var item in site.Items.Where(i => i.Section == department).OrderByDescending(i => i.Offer.Ratings).Take(8))
                html.Append(ProductCard(item));
            html.Append("</div></section>");
        }

        html.Append("<section class=\"h-sec\"><h2>Inspired by your shopping trends</h2><div class=\"h-grid\">");
        foreach (var item in site.Items.Take(Math.Min(12, configuration.Mode.ArticleCount)))
            html.Append(ProductCard(item));
        html.Append("</div></section></div>");
        return Page(site, site.Tagline, html.ToString());
    }

    private string ShopSection(Site site, string section) =>
        ShopListing(site, section, site.Items.Where(i => i.Section == section).OrderByDescending(i => i.Offer.Ratings).ToList(), section,
            "Check each product page for other buying options.");

    private string ShopListing(Site site, string section, List<Item> items, string active, string note)
    {
        var html = new StringBuilder($"<div class=\"h-bar\"><div class=\"h-row\"><span>1-{items.Count} of {items.Count} results for <b>\"{E(section)}\"</b></span><label class=\"h-sort\">Sort by: <select aria-label=\"Sort by\"><option>Featured</option><option>Price: Low to High</option><option>Price: High to Low</option><option>Avg. Customer Review</option><option>Newest Arrivals</option></select></label></div></div>");
        html.Append("<div class=\"h-page h-results\"><aside class=\"h-filters\"><h3>Department</h3>");
        foreach (var department in site.Sections)
            html.Append(department == active ? $"<b>{E(department)}</b>" : $"<a href=\"/{Slug(department)}\">{E(department)}</a>");
        html.Append("<h3>Customer Reviews</h3>");
        foreach (var stars in new[] { 4, 3, 2, 1 })
            html.Append($"<label>{Stars(stars)} &amp; Up</label>");
        html.Append("<h3>Price</h3><label><input type=\"checkbox\"> Under $15</label><label><input type=\"checkbox\"> $15 to $25</label><label><input type=\"checkbox\"> $25 to $50</label><label><input type=\"checkbox\"> $50 &amp; Above</label>");
        html.Append("<h3>Deals &amp; Discounts</h3><label><input type=\"checkbox\"> All Discounts</label><label><input type=\"checkbox\"> Today's Deals</label>");
        html.Append($"<h3>Brand</h3><label><input type=\"checkbox\" checked> {E(site.Name)} Basics</label></aside>");
        html.Append($"<div><h1 class=\"h-h1\">{E(section)}</h1><p class=\"h-sub\">{E(note)}</p><div class=\"h-grid\">");
        foreach (var item in items)
            html.Append(ProductCard(item));
        html.Append("</div></div></div>");
        return Page(site, section, html.ToString(), active);
    }

    private string SearchPage(Site site)
    {
        var html = new StringBuilder("<div class=\"h-bar\"><div class=\"h-row\"><span id=\"sr-count\">Showing all results</span></div></div><div class=\"h-page\"><div class=\"h-sec\"><div class=\"h-grid\">");
        foreach (var item in site.Items)
            html.Append(ProductCard(item));
        html.Append("</div></div></div>");
        html.Append("""
            <script>
            (function () {
              var q = (new URLSearchParams(location.search).get('q') || '').trim();
              var box = document.querySelector('.h-search input');
              if (box) box.value = q;
              var words = q.toLowerCase().split(/\s+/).filter(Boolean);
              var shown = 0;
              document.querySelectorAll('[data-name]').forEach(function (card) {
                var name = card.getAttribute('data-name');
                var match = words.every(function (w) { return name.indexOf(w) >= 0; });
                card.style.display = match ? '' : 'none';
                if (match) shown++;
              });
              if (q) document.getElementById('sr-count').textContent = shown
                ? '1-' + shown + ' of ' + shown + ' results for "' + q + '"'
                : 'No results for "' + q + '". Try checking your spelling or use more general terms.';
            })();
            </script>
            """);
        return Page(site, "Search results", html.ToString());
    }

    private string Cart(Site site)
    {
        var html = new StringBuilder("<div class=\"h-page h-cartpage\"><div class=\"h-sec h-empty\"><div>");
        html.Append($"<h1>Your {E(site.Name)} Cart is empty</h1><p>Your shopping cart lives to serve. Give it purpose: fill it with clothing, kitchenware, supplies and more.</p><a class=\"h-btn\" href=\"/\">Shop today's deals</a></div>");
        html.Append("<div class=\"h-sub-box\"><p>Subtotal (0 items): <b>$0.00</b></p><button class=\"h-cta add\" type=\"button\" disabled>Proceed to checkout</button></div></div>");
        html.Append("<section class=\"h-sec\"><h2>Customers who shopped for basics also bought</h2><div class=\"h-strip\">");
        foreach (var item in site.Items.OrderByDescending(i => i.Offer.Rating).Take(8))
            html.Append(ProductCard(item));
        html.Append("</div></section></div>");
        return Page(site, "Shopping Cart", html.ToString());
    }

    private string Product(Site site, Item item)
    {
        var offer = item.Offer;
        var type = offer.Type;
        var rng = new Random(item.Id ^ world.Seed);
        var images = new[] { offer.Image }.Concat(type.Variants.Select(v => "/img/" + v.Image)).Concat(type.Extra == null ? Array.Empty<string>() : ["/img/" + type.Extra]).Distinct().ToList();
        var off = offer.ListPrice > offer.Price ? (int)Math.Round((1 - offer.Price / offer.ListPrice) * 100) : 0;
        var delivery = world.ReferenceDate.AddDays(3 + rng.Next(3)).ToString("dddd, MMMM d", Us);
        var fastest = world.ReferenceDate.AddDays(1).ToString("MMMM d", Us);

        var html = new StringBuilder($"<div class=\"h-page\"><div class=\"h-crumbs\"><a href=\"/\">{E(site.Name)}</a> › <a href=\"/{Slug(item.Section)}\">{E(item.Section)}</a> › <span>{E(ShortName(item))}</span></div><div class=\"h-pdp\">");
        html.Append("<div class=\"h-gal\">");
        for (var n = 0; n < images.Count; n++)
            html.Append($"<input type=\"radio\" name=\"gal\" id=\"g{n}\"{(n == 0 ? " checked" : "")} aria-label=\"Image {n + 1}\">");
        html.Append("<div class=\"h-thumbs\">");
        for (var n = 0; n < images.Count; n++)
            html.Append($"<label for=\"g{n}\"><img src=\"{images[n]}\" alt=\"\"></label>");
        html.Append("</div><div class=\"h-main\">");
        for (var n = 0; n < images.Count; n++)
            html.Append($"<img class=\"g{n}\" src=\"{images[n]}\" alt=\"{E(item.Headline)}\">");
        html.Append("</div></div>");

        html.Append($"<div class=\"h-info\"><h1>{E(item.Headline)}</h1><div class=\"h-brand\">Brand: {E(site.Name)} Basics</div>");
        html.Append($"<div class=\"h-rate\"><span>{offer.Rating:0.0}</span>{Stars(offer.Rating)}<a href=\"#reviews\">{offer.Ratings:N0} ratings</a>{(offer.Badge == null ? "" : $"<span class=\"h-badge-i\">{E(offer.Badge)}</span>")}</div>");
        html.Append($"<div class=\"h-pp\">{(off > 0 ? $"<span class=\"h-off\">-{off}%</span>" : "")}{Price(offer.Price)}</div>");
        if (off > 0)
            html.Append($"<div class=\"h-list\">List Price: <s>{offer.ListPrice.ToString("C", Us)}</s></div>");
        html.Append("<div class=\"h-note\">FREE Returns · Eligible for free shipping on orders over $35</div>");
        if (type.Variants.Length > 1)
        {
            html.Append($"<div class=\"h-opt\"><b>Color:</b> {E(offer.Color)}<div class=\"h-sw\">");
            foreach (var variant in type.Variants)
                html.Append($"<span class=\"{(variant.Color == offer.Color ? "on" : "")}\"><img src=\"/img/{variant.Image}\" alt=\"{E(variant.Color)}\"></span>");
            html.Append("</div></div>");
        }
        if (type.Sizes != null)
        {
            html.Append("<div class=\"h-opt\"><b>Size:</b><div class=\"h-sizes\">");
            foreach (var size in type.Sizes)
                html.Append($"<span>{E(size)}</span>");
            html.Append("</div></div>");
        }
        html.Append("<div class=\"h-about\"><h2>About this item</h2><ul>");
        foreach (var feature in type.Features)
            html.Append($"<li>{E(feature)}</li>");
        html.Append("</ul></div></div>");

        html.Append($"<div class=\"h-box\">{Price(offer.Price)}<p class=\"h-del\"><b>FREE delivery {delivery}</b> on orders over $35. Or fastest delivery <b>Tomorrow, {fastest}</b>.</p>");
        html.Append($"<p class=\"h-loc2\">{Icon("pin")} Deliver to {E(world.City)} {Zip}</p><p class=\"h-instock\">In Stock</p>");
        html.Append("<label class=\"h-qty\">Quantity: <select aria-label=\"Quantity\"><option>1</option><option>2</option><option>3</option><option>4</option><option>5</option></select></label>");
        html.Append("<button class=\"h-cta add\" type=\"button\">Add to Cart</button><button class=\"h-cta buy\" type=\"button\">Buy Now</button>");
        html.Append($"<dl><dt>Ships from</dt><dd>{E(site.Name)}</dd><dt>Sold by</dt><dd>{E(site.Name)}</dd><dt>Returns</dt><dd>30-day refund or replacement</dd><dt>Payment</dt><dd>Secure transaction</dd></dl></div></div>");

        html.Append("<div class=\"h-sec\"><h2>Product information</h2><table class=\"h-spec\"><tbody>");
        foreach (var (key, value) in type.Specs.Concat([("Department", item.Section), ("Item model number", $"{Slug(item.Section).ToUpperInvariant()[..3]}-{item.Id}"), ("Date first available", item.Published.ToString("MMMM d, yyyy", Us)), ("Customer reviews", $"{offer.Rating:0.0} out of 5 stars ({offer.Ratings:N0})")]))
            html.Append($"<tr><th>{E(key)}</th><td>{E(value)}</td></tr>");
        html.Append($"</tbody></table><h2 class=\"h-h2b\">Product description</h2><p class=\"h-desc\">{E(type.Blurb)} Designed and tested by the {E(site.Name)} team and backed by our 30-day return promise.</p></div>");

        html.Append(Reviews(item, rng));

        html.Append("<section class=\"h-sec\"><h2>Products related to this item</h2><div class=\"h-strip\">");
        foreach (var other in site.Items.Where(i => i.Section == item.Section && i != item).Concat(site.Items.Where(i => i.Section != item.Section)).Take(8))
            html.Append(ProductCard(other));
        html.Append("</div></section></div>");
        return Page(site, item.Headline, html.ToString(), item.Section);
    }

    private string Reviews(Item item, Random rng)
    {
        var offer = item.Offer;
        // a share of five-star ratings that fits the average, the rest falling off by star
        var five = Math.Clamp((int)Math.Round((offer.Rating - 3.4) * 50), 15, 88);
        var four = (100 - five) * 5 / 10;
        var three = (100 - five - four) * 5 / 10;
        var two = (100 - five - four - three) / 2;
        var shares = new[] { five, four, three, two, 100 - five - four - three - two };

        var html = new StringBuilder($"<div class=\"h-sec h-revs\" id=\"reviews\"><div><h2>Customer reviews</h2><div class=\"h-rate\">{Stars(offer.Rating)}<b>{offer.Rating:0.0} out of 5</b></div><p class=\"h-sub\">{offer.Ratings:N0} global ratings</p><div class=\"h-hist\">");
        for (var n = 0; n < 5; n++)
            html.Append($"<div><span>{5 - n} star</span><span class=\"h-meter\"><i style=\"width:{shares[n]}%\"></i></span><span>{shares[n]}%</span></div>");
        html.Append("</div></div><div><h3>Top reviews from the United States</h3>");
        var reviews = World.Shuffle(rng, offer.Type.Reviews).OrderByDescending(r => r.Stars >= 4).ToList();
        foreach (var (stars, title, text) in reviews)
        {
            var who = $"{Pick(rng, LocalFirst)} {Pick(rng, LocalLast)[0]}.";
            var sizes = offer.Type.Sizes;
            var said = text.Contains("medium") ? "M" : text.Contains("large") ? "L" : text.Contains("small") ? "S" : null;
            var size = sizes == null ? "" : $"Size: {(said == null ? sizes[1 + rng.Next(sizes.Length - 2)] : sizes.First(z => z.StartsWith(said)))} | ";
            html.Append($"<div class=\"h-rev\"><div class=\"h-who\"><span class=\"h-av\">{Icon("user")}</span>{E(who)}</div><div class=\"h-rt\">{Stars(stars)} <b>{E(title)}</b></div>");
            html.Append($"<div class=\"h-rd\">Reviewed in the United States on {world.ReferenceDate.AddDays(-rng.Next(3, 240)).ToString("MMMM d, yyyy", Us)}</div>");
            html.Append($"<div class=\"h-rd\">{E(size)}Color: {E(offer.Color)} | <span class=\"h-vp\">Verified Purchase</span></div><p>{E(text)}</p>");
            html.Append($"<div class=\"h-rd\">{rng.Next(2, 140)} people found this helpful</div></div>");
        }
        return html.Append("</div></div>").ToString();
    }

    private static string ProductCard(Item item)
    {
        var offer = item.Offer;
        var off = offer.ListPrice > offer.Price ? (int)Math.Round((1 - offer.Price / offer.ListPrice) * 100) : 0;
        var html = new StringBuilder($"<div class=\"h-card\" data-name=\"{E($"{item.Headline} {item.Section}".ToLowerInvariant())}\">");
        if (offer.Badge != null)
            html.Append($"<span class=\"h-badge{(offer.Badge == "Limited time deal" ? " deal" : "")}\">{E(offer.Badge)}</span>");
        html.Append($"<a class=\"h-img\" href=\"/product/{item.Id}\"><img src=\"{item.Image}\" alt=\"{E(item.Headline)}\"></a>");
        html.Append($"<a class=\"h-title\" href=\"/product/{item.Id}\">{E(item.Headline)}</a>");
        html.Append($"<div class=\"h-rate\">{Stars(offer.Rating)}<span class=\"h-rc\">{offer.Ratings:N0}</span></div>");
        if (off > 0)
            html.Append($"<div><span class=\"h-dealtag\">{off}% off</span> <span class=\"h-dealtxt\">{(off >= 25 ? "Limited time deal" : "Deal")}</span></div>");
        html.Append($"<div>{Price(offer.Price)}{(off > 0 ? $" <span class=\"h-list\">List: <s>{offer.ListPrice.ToString("C", Us)}</s></span>" : "")}</div>");
        html.Append("<div class=\"h-ship\">FREE delivery on $35 shipped by us</div></div>");
        return html.ToString();
    }

    private static string ShortName(Item item) => item.Headline[(item.Headline.IndexOf(" Basics ", StringComparison.Ordinal) + 8)..].Split(',')[0];

    private static string Price(decimal price) =>
        $"<span class=\"h-price\"><sup>$</sup>{Math.Floor(price):0}<sup>{(int)(price % 1 * 100):00}</sup></span>";

    private static string Stars(double rating)
    {
        var label = rating.ToString("0.0", Us);
        return $"<span class=\"stars\" role=\"img\" aria-label=\"{label} out of 5 stars\"><span style=\"width:{rating / 5 * 100:0}%\">★★★★★</span>★★★★★</span>";
    }

    private string Zip => (10000 + world.Seed % 89999).ToString("00000");

    // ---------- info pages ----------

    private string InfoPage(Site site, string page)
    {
        var city = world.City;
        var domain = SiteDomain(site);
        var shop = site.Type == "shopping";
        var founded = world.ReferenceDate.Year - 20 - world.Seed % 30;
        var address = $"{world.Seed % 800 + 100} Main Street, {city} {Zip}";
        var editor = site.Writers.FirstOrDefault();
        var (title, html) = page switch
        {
            "about" => (shop ? $"About {site.Name}" : "About Us", shop
                ? $"<p>{E(site.Name)} started in {founded} as a small shop in {E(city)} selling cotton tees and enamel mugs out of a converted garage. Today we ship everyday basics to customers in all 50 states.</p>" +
                  $"<h2>What we believe</h2><p>Good basics should be well made, fairly priced and built to last. Every {E(site.Name)} Basics product is wear-tested by our team before it goes on sale, and if it doesn't hold up, we take it back.</p>" +
                  "<h2>Our promise</h2><ul><li>Free shipping on orders over $35</li><li>Free returns within 30 days</li><li>Real people answering the phone, seven days a week</li></ul>" +
                  $"<h2>Visit us</h2><p>Our flagship store and fulfillment center are at {E(address)}.</p>"
                : $"<p>{E(site.Name)} has covered {E(city)} and the surrounding region since {founded}. Our newsroom is independent and locally owned, and our journalists live in the communities they cover.</p>" +
                  "<h2>Our mission</h2><p>We report the news fairly and accurately, hold the powerful to account and help readers understand the place they live. We correct our mistakes promptly and in public.</p>" +
                  $"<h2>Our newsroom</h2><p>{(editor == null ? "" : $"{E(editor.Name)} leads a staff of reporters, editors, photographers and producers. ")}Reporters cover local government, schools, business, health, sports and culture, and our opinion section is separate from the newsroom.</p>" +
                  $"<h2>Ethics and standards</h2><p>Staff members may not accept gifts, free travel or payment from people they cover. Advertisers have no say in our coverage. Read our <a class=\"info-link\" href=\"/terms\">terms</a> and <a class=\"info-link\" href=\"/privacy\">privacy policy</a>.</p>"),
            "contact" => (shop ? "Customer Service" : "Contact Us", shop
                ? $"<p>We're here to help every day from 8 a.m. to 8 p.m. Eastern.</p><h2>Phone</h2><p>{Phone}</p><h2>Email</h2><p>support@{E(domain)}. We reply within one business day.</p>" +
                  "<h2>Orders and shipping</h2><p>Most orders ship within one business day. Standard delivery takes 3 to 5 business days; expedited delivery is available at checkout.</p>" +
                  "<h2>Returns</h2><p>Return any unworn, unwashed item within 30 days for a full refund. Start a return by email or phone and we'll send a prepaid label.</p>" +
                  $"<h2>Mailing address</h2><p>{E(site.Name)} Customer Care, {E(address)}</p>"
                : $"<h2>News tips</h2><p>Have a story idea or a tip? Email tips@{E(domain)} or call the newsroom at {Phone}.</p>" +
                  $"<h2>Corrections</h2><p>To report an error, email corrections@{E(domain)}. Please include the headline and date of the story.</p>" +
                  $"<h2>Letters to the editor</h2><p>Send letters of 250 words or fewer to letters@{E(domain)}. Include your name, neighborhood and a phone number for verification.</p>" +
                  $"<h2>Subscriptions and delivery</h2><p>Customer service: {Phone}, weekdays 7 a.m. to 5 p.m., weekends 7 a.m. to noon.</p>" +
                  $"<h2>Advertising</h2><p>ads@{E(domain)}</p><h2>Office</h2><p>{E(address)}</p>"),
            "privacy" => (shop ? "Privacy Notice" : "Privacy Policy",
                $"<p class=\"updated\">Last updated {world.ReferenceDate.AddDays(-140).ToString("MMMM d, yyyy", Us)}</p>" +
                $"<p>This policy explains what information {E(site.Name)} collects, how we use it and the choices you have.</p>" +
                $"<h2>Information we collect</h2><p>{(shop ? "When you place an order, we collect your name, shipping and billing addresses, email address, phone number and payment information." : "When you subscribe or register, we collect your name, email address and, for print delivery, your mailing address.")} We also collect information about how you use the site, such as the pages you visit and the device and browser you use.</p>" +
                $"<h2>How we use it</h2><p>We use this information to {(shop ? "process and deliver orders, handle returns" : "deliver your subscription, send newsletters you request")}, prevent fraud, improve the site and, where you allow it, send you offers. We do not sell your personal information.</p>" +
                "<h2>Cookies</h2><p>We use cookies to keep you signed in, remember your preferences and measure how the site is used. You can control cookies in your browser settings.</p>" +
                $"<h2>Your choices</h2><p>You can unsubscribe from marketing email at any time, and you may request a copy of your information or ask us to delete it by writing to privacy@{E(domain)}.</p>" +
                $"<h2>Contact</h2><p>Questions about this policy can be sent to privacy@{E(domain)} or {E(address)}.</p>"),
            _ => (shop ? "Conditions of Use" : "Terms of Service",
                $"<p class=\"updated\">Last updated {world.ReferenceDate.AddDays(-140).ToString("MMMM d, yyyy", Us)}</p>" +
                $"<p>By using this site, you agree to these terms. Please read them carefully.</p>" +
                $"<h2>{(shop ? "Orders and pricing" : "Use of content")}</h2><p>{(shop ? "All orders are subject to availability and acceptance. Prices and promotions may change without notice. If an item is listed at an incorrect price, we may cancel the order and refund your payment." : $"Content on this site is for personal, non-commercial use. You may share links to our stories, but republishing articles, photos or graphics requires written permission from {E(site.Name)}.")}</p>" +
                "<h2>Accounts</h2><p>You are responsible for keeping your password confidential and for all activity under your account. Tell us right away if you suspect unauthorized use.</p>" +
                $"<h2>{(shop ? "Returns" : "Comments and submissions")}</h2><p>{(shop ? "Our return policy is part of these terms. See Customer Service for details." : "If you submit a comment, letter or photo, you grant us permission to publish and edit it. Comments that are abusive, off-topic or promotional may be removed.")}</p>" +
                $"<h2>Disclaimer</h2><p>The site is provided as is. To the extent permitted by law, {E(site.Name)} is not liable for indirect or consequential damages arising from its use.</p>" +
                $"<h2>Governing law</h2><p>These terms are governed by the laws of the state in which {E(site.Name)} is headquartered.</p>")
        };
        return Page(site, title, $"<div class=\"info\"><h1>{E(title)}</h1>{html}</div>");
    }

    private static string SiteDomain(Site site) => $"{Slug(site.Name)}.example";

    #endregion

    #region Pages for any path

    // route and file words that say nothing about what a page is about
    private static readonly HashSet<string> RouteWords =
    [
        "article", "articles", "news", "story", "stories", "post", "posts", "blog", "blogs", "product", "products", "p", "dp", "gp", "item", "items",
        "category", "categories", "c", "page", "pages", "index", "default", "home", "www", "en", "us", "amp", "php", "aspx", "asp", "jsp", "cfm",
        "id", "view", "detail", "details", "s", "shop", "store", "catalog", "section", "sections", "topic", "topics", "tag", "tags", "author", "authors"
    ];

    private static readonly HashSet<string> Acronyms =
        ["it", "hr", "pr", "ceo", "cfo", "cio", "cto", "vpn", "erp", "api", "ai", "tv", "usa", "uk", "eu", "us", "nfl", "nba", "mlb", "nhl", "mls", "ncaa", "pga", "fbi", "nasa", "dc", "nyc", "la"];

    private static readonly Dictionary<string, string> NewsTopics = new()
    {
        ["tech"] = "Technology", ["cyber"] = "Technology", ["security"] = "Technology", ["outage"] = "Technology", ["software"] = "Technology", ["network"] = "Technology",
        ["phishing"] = "Technology", ["ai"] = "Technology", ["computer"] = "Technology", ["internet"] = "Technology", ["data"] = "Technology", ["app"] = "Technology",
        ["election"] = "Politics", ["council"] = "Politics", ["mayor"] = "Politics", ["vote"] = "Politics", ["budget"] = "Politics", ["governor"] = "Politics", ["tax"] = "Politics", ["taxes"] = "Politics",
        ["economy"] = "Business", ["jobs"] = "Business", ["careers"] = "Business", ["career"] = "Business", ["earnings"] = "Business", ["market"] = "Business", ["markets"] = "Business",
        ["company"] = "Business", ["stocks"] = "Business", ["hiring"] = "Business", ["retail"] = "Business",
        ["storm"] = "Local", ["weather"] = "Local", ["flood"] = "Local", ["fire"] = "Local", ["police"] = "Local", ["school"] = "Local", ["schools"] = "Local", ["traffic"] = "Local", ["festival"] = "Local",
        ["hospital"] = "Health", ["flu"] = "Health", ["covid"] = "Health", ["vaccine"] = "Health", ["clinic"] = "Health", ["doctor"] = "Health",
        ["climate"] = "World", ["war"] = "World", ["europe"] = "World", ["global"] = "World", ["space"] = "Science", ["research"] = "Science", ["study"] = "Science",
        ["game"] = "Sports", ["team"] = "Sports", ["coach"] = "Sports", ["football"] = "Sports", ["basketball"] = "Sports", ["baseball"] = "Sports", ["soccer"] = "Sports",
        ["film"] = "Arts", ["movie"] = "Arts", ["music"] = "Arts", ["concert"] = "Arts", ["theater"] = "Arts", ["museum"] = "Arts", ["editorial"] = "Opinion", ["column"] = "Opinion",
        ["federal"] = "Nation", ["national"] = "Nation", ["washington"] = "Nation"
    };

    private static readonly Dictionary<string, string> SportsTopics = new()
    {
        ["nfl"] = "Football", ["quarterback"] = "Football", ["touchdown"] = "Football", ["nba"] = "Basketball", ["dunk"] = "Basketball", ["mlb"] = "Baseball", ["pitcher"] = "Baseball",
        ["mls"] = "Soccer", ["goal"] = "Soccer", ["nhl"] = "Hockey", ["puck"] = "Hockey", ["ncaa"] = "College", ["recruit"] = "College", ["pga"] = "Golf", ["open"] = "Tennis"
    };

    private static readonly Dictionary<string, string> EntertainmentTopics = new()
    {
        ["films"] = "Film", ["movie"] = "Film", ["movies"] = "Film", ["trailer"] = "Film", ["show"] = "TV", ["series"] = "TV", ["season"] = "TV", ["episode"] = "TV",
        ["album"] = "Music", ["song"] = "Music", ["tour"] = "Music", ["concert"] = "Music", ["band"] = "Music", ["singer"] = "Music",
        ["star"] = "Celebrity", ["couple"] = "Celebrity", ["wedding"] = "Celebrity", ["dating"] = "Celebrity", ["stream"] = "Streaming", ["watch"] = "Streaming",
        ["box"] = "Box Office", ["office"] = "Box Office", ["review"] = "Reviews", ["award"] = "Awards", ["oscars"] = "Awards", ["emmys"] = "Awards", ["grammys"] = "Awards"
    };

    // a page for a path no link leads to, made from the path's words and seeded by the path, so it never changes
    private string PathPage(Site site, string[] segments)
    {
        var rng = new Random(unchecked((int)ContentRequest.StableHash(string.Join('/', segments)) ^ world.Seed));
        var words = PathWords(segments);
        var named = words.Count == 0 ? null : site.Sections.FirstOrDefault(s => Slug(s) == string.Join('-', words));
        if (named != null)
            return SectionPage(site, named);
        if (site.Type == "shopping")
            return ShopPathPage(site, segments, words, rng);

        var section = PathSection(site, segments, rng);
        if (segments.Length == 2 && segments[0] == "author" && words.Count > 0)
            return AuthorPage(site, new Writer(string.Join(' ', words.Take(3).Select(Cap)), section, null));
        return Article(site, PathStory(site, segments, words, section, rng));
    }

    // the words of the last segment that has any, else of the whole path, without route words, years or numbers
    private static List<string> PathWords(string[] segments)
    {
        static List<string> Words(string segment) =>
            segment.Split('-', '_', '.', '+', ' ').Where(w => w.Length > 0 && w.All(char.IsLetter))
                .Where(w => !RouteWords.Contains(w) && (w.Length > 1 || w == "a")).ToList();
        return segments.Reverse().Select(Words).FirstOrDefault(w => w.Count > 0) ?? [];
    }

    private string PathSection(Site site, string[] segments, Random rng)
    {
        var all = segments.SelectMany(s => Regex.Matches(s, "[a-z]+").Select(m => m.Value)).ToList();
        var topics = site.Type switch { "sports" => SportsTopics, "entertainment" => EntertainmentTopics, _ => NewsTopics };
        return site.Sections.FirstOrDefault(s => segments.Contains(Slug(s)) || all.Contains(Slug(s)))
               ?? all.Select(w => topics.GetValueOrDefault(w)).FirstOrDefault(t => t != null)
               ?? Pick(rng, site.Sections.Where(s => s != "Opinion").ToList());
    }

    // a date in the path, as in /news/2026/11/04/storm-cleanup, never later than the reference date
    private DateTime PathDate(string[] segments, Random rng)
    {
        var now = world.ReferenceDate.AddHours(12);
        var fallback = now.AddMinutes(-rng.Next(30, 60 * 24 * 3));
        var at = Array.FindIndex(segments, s => s.Length == 4 && int.TryParse(s, out var y) && y is >= 1990 and <= 2100);
        if (at < 0)
            return fallback;
        var year = int.Parse(segments[at]);
        var month = at + 1 < segments.Length && int.TryParse(segments[at + 1], out var m) && m is >= 1 and <= 12 ? m : 0;
        var day = month > 0 && at + 2 < segments.Length && int.TryParse(segments[at + 2], out var d) && d >= 1 && d <= DateTime.DaysInMonth(year, month) ? d : 0;
        var start = new DateTime(year, Math.Max(month, 1), Math.Max(day, 1));
        var end = day > 0 ? start : month > 0 ? start.AddMonths(1).AddDays(-1) : new DateTime(year, 12, 31);
        if (start > now)
            return fallback;
        if (end > now)
            end = now.Date;
        var date = start.AddDays(rng.Next((end - start).Days + 1)).AddHours(rng.Next(6, 20)).AddMinutes(rng.Next(60));
        return date > now ? now.AddMinutes(-rng.Next(30, 300)) : date;
    }

    // "storm cleanup" stays lower case except for names and acronyms, so it reads inside a sentence
    private string Phrase(List<string> words)
    {
        var names = new HashSet<string>(TeamCities.Append(world.City).Concat(world.OrganizationName.Split(' '))
            .Concat(CultureInfo.InvariantCulture.DateTimeFormat.MonthNames).Concat(CultureInfo.InvariantCulture.DateTimeFormat.DayNames)
            .Where(n => n.Length > 0).Select(n => n.ToLowerInvariant()));
        return string.Join(' ', words.Select(w => Acronyms.Contains(w) ? w.ToUpperInvariant() : names.Contains(w) ? Cap(w) : w));
    }

    private Item PathStory(Site site, string[] segments, List<string> words, string section, Random rng)
    {
        var type = site.Type;
        var published = PathDate(segments, rng);
        var facts = NewFacts(type, section, published, rng);
        var beat = site.Writers.Where(w => w.Beat == section).ToList();
        var author = beat.Count > 0 ? Pick(rng, beat) : Pick(rng, site.Writers);
        var id = segments.Length == 2 && int.TryParse(segments[1], out var n) && n > 0 ? n : 100000 + rng.Next(900000);

        // no words to go on: one of the section's own stories, about the path's own people and places
        if (words.Count == 0)
        {
            var templates = type switch { "sports" => SportsStories(section), "entertainment" => EntertainmentStories(section), _ => NewsStories(section) };
            var template = Pick(rng, templates);
            var story = template(facts);
            var image = story.Theme is "match" or "team" ? story.Theme : PickPhoto(story.Theme, new Dictionary<string, int>(), rng);
            if (type == "entertainment" && FitStarToPhoto(facts, image) is var fitted && fitted != facts)
                story = template(facts = fitted);
            return new Item(id, section, story.Kind, story.Headline, story.Dek, author, published, image, facts, story.Lines, null);
        }

        // a long slug is the headline itself; a short one is the topic of a headline
        var phrase = Phrase(words);
        var sentence = words.Count >= 4;
        var topic = sentence ? "the news" : phrase;
        var city = world.City;
        var day = facts.Day;
        string headline, dek, kind, theme;
        string[] lines;
        switch (type)
        {
            case "sports":
                var individual = section is "Golf" or "Tennis";
                var coach = section is "Soccer" or "Baseball" ? "manager" : "coach";
                headline = sentence ? Cap(phrase) : Pick(rng, [$"{Cap(phrase)}: what it means for the rest of the season", $"Everything to know about {phrase}", $"{Cap(phrase)} takes center stage this week"]);
                dek = individual ? $"Players at the {facts.Team.City} {(section == "Golf" ? "Classic" : "Open")} weigh in." : $"What it means for {The(facts.Team)} and the rest of the season.";
                kind = individual ? "individual" : "team";
                theme = individual ? (section == "Golf" ? "course" : "athlete") : Pick(rng, ["fans", "athlete", "team"]);
                var lede = sentence ? $"{Cap(phrase)}, and" : $"{Cap(phrase)} has become the storyline of the week, and";
                lines = individual
                    ? [
                        $"{facts.Team.City.ToUpperInvariant()} — {lede} players at the {facts.Team.City} {(section == "Golf" ? "Classic" : "Open")} were asked about it at nearly every news conference on {day}.",
                        $"\"Everybody has an opinion, and that's fine,\" said {facts.Star}. \"I'm just trying to take care of my own game.\"",
                        "## What players are saying",
                        $"{facts.Star2} said the conversation was good for the sport. \"People care about this, and that's a good thing,\" {L(facts.Star2)} said.",
                        $"Tour officials said they would address {topic} with players before the next event."
                    ]
                    : [
                        $"{facts.Team.City.ToUpperInvariant()} — {lede} nobody inside {The(facts.Team)} locker room is pretending otherwise.",
                        $"The subject came up again {day} after practice, when {coach} {facts.Local} spent several minutes answering questions about {topic}.",
                        $"\"It's part of the job. We talk about it, we deal with it, and then we go play,\" {L(facts.Local)} said.",
                        "## Inside the locker room",
                        $"{facts.Star}, one of the team's veteran leaders, said the players were focused on the next game. \"We control what we can control,\" {L(facts.Star)} said."
                    ];
                break;
            case "entertainment":
                headline = sentence ? Cap(phrase) : Pick(rng, [$"Everything we know about {phrase}", $"{Cap(phrase)}: why everyone is talking about it", $"The internet can't stop talking about {phrase}"]);
                dek = $"Here's everything we know so far about {topic}.";
                kind = "industry";
                theme = section switch { "Music" => "stage", "Streaming" => "streaming", "Box Office" => "nightlife", _ => "portrait" };
                lines = [];
                break;
            default:
                var namesOrg = words.Contains(world.ShortName.ToLowerInvariant());
                var onOrg = namesOrg || section == "Technology" && words.Any(w => w is "outage" or "breach" or "phishing" or "network" or "systems" or "email" or "portal" or "login" or "password" or "service" or "incident");
                var abroad = section is "World" or "Nation";
                headline = sentence ? Cap(phrase)
                    : namesOrg ? Pick(rng, [$"{Cap(phrase)}: what we know so far", $"What {Customers} should know about the {phrase}"])
                    : onOrg ? Pick(rng, [$"{world.ShortName} responds to questions about {phrase}", $"What {world.ShortName} {Customers} should know about {phrase}"])
                    : Pick(rng, [$"{Cap(phrase)}: what we know so far", $"What {(abroad ? "to know" : $"{city} needs to know")} about {phrase}", $"Officials respond to questions about {phrase}"]);
                dek = $"Here is what we know about {topic} so far, and what happens next.";
                kind = onOrg ? "org" : abroad ? "nation" : "civic";
                theme = section switch
                {
                    "Local" => "community", "Nation" or "Politics" => "city", "World" => "travel", "Business" => "office", "Technology" => "tech", "Science" => "wildlife",
                    "Health" => "health", "Sports" => "fans", "Arts" => "stage", _ => "city"
                };
                lines = [];
                break;
        }

        var photo = theme == "team" ? "team" : PickPhoto(theme, new Dictionary<string, int>(), rng);
        if (type == "entertainment")
            facts = FitStarToPhoto(facts, photo);
        if (lines.Length == 0)
            lines = type == "entertainment" ? EntertainmentPathLines(facts, phrase, sentence) : NewsPathLines(facts, phrase, sentence, kind, section, rng);
        return new Item(id, section, kind, headline, dek, author, published, photo, facts, lines, null);
    }

    private string[] NewsPathLines(Facts f, string phrase, bool sentence, string kind, string section, Random rng)
    {
        var city = world.City;
        var topic = sentence ? "the news" : phrase;
        var (sp, cio) = (Spokesperson, Cio);
        if (kind == "org")
            return
            [
                $"{city.ToUpperInvariant()} — {(sentence ? $"{Cap(phrase)}, {world.OrganizationName} confirmed {f.Day}," : $"{Cap(phrase)} has drawn questions from {Customers} and employees of {world.OrganizationName} this week, and the {OrgNoun} said {f.Day}")} that it was working to share more information as quickly as it could.",
                $"In a statement, {Of(sp)}, said the {OrgNoun} was aware of the questions and was reviewing the matter with the teams involved.",
                $"\"We take this seriously, and we will be open about what we learn,\" {sp.LastName} said. \"Our {Customers} deserve clear answers.\"",
                "## What we know",
                $"Details were still emerging {f.Day} evening. {Of(cio)}, told staff in an internal message that teams were working through the issue \"methodically and carefully.\"",
                $"{f.Local}, who has done business with the {OrgNoun} for more than a decade, said it had been responsive so far. \"I called, and someone picked up,\" {L(f.Local)} said. \"That counts for a lot.\""
            ];
        if (kind == "nation")
            return
            [
                $"{(section == "World" ? Pick(rng, ["LONDON", "GENEVA", "SINGAPORE", "BRUSSELS"]) : "WASHINGTON")} — {(sentence ? $"{Cap(phrase)}, officials said {f.Day}," : $"{Cap(phrase)} moved to the center of the debate this week,")} as officials and analysts weighed what it could mean for the months ahead.",
                "Officials have not released a full timeline, but people familiar with the discussions said a decision could come within weeks.",
                $"\"There's a lot of noise and not a lot of detail yet,\" said {f.Local}, a senior analyst at the Halvorsen Institute. \"The details will matter.\"",
                "## What to watch",
                $"Further announcements on {topic} are expected in the coming days, and lawmakers have asked for a briefing.",
                $"{f.Local2}, a researcher who has followed the issue for years, said the attention was overdue. \"This has been building for a long time,\" {L(f.Local2)} said."
            ];
        return
        [
            $"{city.ToUpperInvariant()} — {(sentence ? $"{Cap(phrase)}, city officials said {f.Day}," : $"{Cap(phrase)} has become one of the most talked-about issues in {city} this week,")} as residents, officials and business owners weigh what it means for the region.",
            $"The subject came up repeatedly at a public meeting on {f.Day}, where more than {f.Number % 40 + 20} people signed up to speak, and city staff said they had fielded dozens of calls and emails.",
            $"\"People want straight answers, and they deserve them,\" said council member {f.Local}. \"We're still gathering the facts.\"",
            "## What we know",
            $"City officials said they expected to share more information about {topic} in the coming days, and that a written summary would be posted on the city's website.",
            $"{f.Local2}, who has lived in {city} for more than 20 years, said the discussion was overdue. \"This affects all of us, whether we realize it yet or not,\" {L(f.Local2)} said."
        ];
    }

    private static string[] EntertainmentPathLines(Facts f, string phrase, bool sentence) =>
    [
        sentence ? $"{Cap(phrase)}, and the entertainment world is still catching up." : $"{Cap(phrase)} is the story everyone in entertainment is talking about this week, and the conversation shows no sign of slowing down.",
        $"The buzz started {f.Day}, when {f.Star} addressed it in an interview, and it quickly became one of the most-searched topics of the day.",
        $"\"I didn't expect it to take off the way it did, but I'm glad people are paying attention,\" {L(f.Star)} said.",
        "## Why it matters",
        $"Industry watchers say the moment reflects a bigger shift. \"Audiences want to feel like they're part of the conversation,\" said {f.Local}, a media analyst.",
        $"Representatives for {f.Network} and {f.Studio} did not immediately respond to requests for comment."
    ];

    // a product the path names, or else a category page of what the store has that matches
    private string ShopPathPage(Site site, string[] segments, List<string> words, Random rng)
    {
        var productRoute = segments.Length > 1 && segments[0] is "product" or "products" or "p" or "dp" or "gp" or "item" or "items";
        var type = ProductFor(words);
        if (type != null || productRoute && words.Count == 0)
            return Product(site, PathProduct(site, segments, words, type ?? Pick(rng, Catalog), rng));

        var title = words.Count > 0 ? TitleCase(words) : "Featured Products";
        var stems = words.Where(w => w.Length > 2).Select(w => w.TrimEnd('s')).ToList();
        var matches = site.Items.Where(i => stems.Any(w => i.Headline.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
        var note = matches.Count > 0 ? "Check each product page for other buying options." : "No exact matches. Here are some of our most popular items instead.";
        var items = matches.Count > 0 ? matches : site.Items.OrderBy(_ => rng.Next()).Take(12).ToList();
        return ShopListing(site, title, items, null, note);
    }

    private static ProductType ProductFor(List<string> words)
    {
        bool Has(params string[] any) => words.Any(any.Contains);
        var department = Has("tee", "tees", "tshirt", "shirt", "shirts", "top", "tops")
            ? Has("women", "womens", "ladies") ? "Women" : Has("kids", "kid", "boys", "girls", "youth", "toddler") ? "Kids" : "Men"
            : Has("mug", "mugs", "cup", "cups")
                ? Has("camp", "camping", "backpacking", "trail", "outdoor") ? "Outdoor" : "Kitchen & Dining"
                : Has("notebook", "notebooks", "journal", "pad", "sketchbook", "steno") ? "Office & School"
                : Has("tote", "totes", "bag", "bags") ? Has("beach", "trail", "outdoor") ? "Outdoor" : "Bags & Totes"
                : null;
        if (department == null)
            return null;
        // ceramic mugs and enamel mugs are separate products in the same department
        var types = Catalog.Where(t => t.Department == department && (department != "Outdoor" || t.Names[0].Contains(Has("tote", "totes", "bag", "bags") ? "Tote" : "Mug"))).ToList();
        return Has("ceramic", "coffee", "latte", "diner", "stoneware") && types.Count > 1 ? types[1] : types[0];
    }

    private Item PathProduct(Site site, string[] segments, List<string> words, ProductType type, Random rng)
    {
        var variant = type.Variants.FirstOrDefault(v => words.Contains(v.Color.ToLowerInvariant()));
        if (variant.Color == null)
            variant = Pick(rng, type.Variants);
        var name = ProductFor(words) != null ? TitleCase(words.Where(w => w != variant.Color.ToLowerInvariant()).ToList()) : Pick(rng, type.Names);
        var price = rng.Next(type.Low, type.High + 1) + (rng.Next(3) == 0 ? 0.49m : 0.99m);
        var listPrice = rng.Next(100) < 50 ? Math.Floor(price * (1 + rng.Next(12, 40) / 100m)) + 0.99m : price;
        var id = int.TryParse(segments[^1], out var n) && n > 0 ? n : 100000 + rng.Next(900000);
        var offer = new Offer(type, variant.Color, "/img/" + variant.Image, price, listPrice, Math.Round(3.8 + rng.NextDouble() * 1.1, 1), rng.Next(40, 6000),
            rng.Next(4) == 0 ? "Top Rated" : null);
        return new Item(id, type.Department, "product", $"{site.Name} Basics {name}{(type.Variants.Length > 1 ? $", {variant.Color}" : "")}", type.Blurb, null,
            world.ReferenceDate.AddDays(-rng.Next(20, 700)).AddHours(12), offer.Image, null, type.Features, offer);
    }

    private static string ApDate(DateTime date) =>
        (date.Month switch { 3 => "March", 4 => "April", 5 => "May", 6 => "June", 7 => "July", 9 => "Sept.", _ => date.ToString("MMM", Us) + "." }) + date.ToString(" d, yyyy", Us);

    private static string TitleCase(List<string> words) =>
        string.Join(' ', words.Select((w, i) => Acronyms.Contains(w) ? w.ToUpperInvariant()
            : i > 0 && w is "a" or "an" or "and" or "for" or "in" or "of" or "on" or "or" or "the" or "to" or "with" ? w : Cap(w)));

    #endregion

    #region Chrome

    private string Page(Site site, string title, string content, string active = null)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.Append($"<title>{E(title)} - {E(site.Name)}</title><meta name=\"description\" content=\"{E(site.Tagline)}\">");
        html.Append($"<style>:root{{--accent:{site.Accent}}}{BaseCss}{site.Type switch { "shopping" => ShopCss, "sports" => SportsCss, "entertainment" => EntertainmentCss, _ => NewsCss }}</style></head>");
        html.Append($"<body class=\"{site.Type}\" id=\"top\">");
        html.Append(site.Type switch { "shopping" => ShopHeader(site, active), "sports" => SportsHeader(site, active), "entertainment" => EntertainmentHeader(site, active), _ => NewsHeader(site, active) });
        html.Append("<main>").Append(content).Append("</main>");
        html.Append(Footer(site));
        html.Append("</body></html>");
        return html.ToString();
    }

    private string Nav(Site site, string active) =>
        string.Concat(site.Sections.Select(s => $"<a href=\"/{Slug(s)}\"{(s == active ? " class=\"on\"" : "")}>{E(s)}</a>"));

    private string NewsHeader(Site site, string active)
    {
        var temps = new[] { 34, 38, 47, 58, 68, 77, 82, 80, 73, 61, 50, 39 };
        var temp = temps[world.ReferenceDate.Month - 1] + world.Seed % 9 - 4;
        var sky = new[] { "Mostly cloudy", "Partly sunny", "Clear", "Light rain", "Breezy" }[world.Seed % 5];
        return "<div class=\"n-topbar n-ui\"><div class=\"wrap\"><div class=\"n-ed\"><span>U.S.</span><span>International</span><span>Canada</span><span>Español</span></div>" +
               "<div class=\"n-acct\"><button class=\"n-btn\" type=\"button\">Subscribe for $1/week</button><button class=\"n-btn alt\" type=\"button\">Log in</button></div></div></div>" +
               $"<header class=\"n-mast\"><div class=\"wrap\"><div class=\"n-date n-ui\">{world.ReferenceDate.ToString("dddd, MMMM d, yyyy", Us)}<small>Today's Paper</small></div>" +
               $"<a class=\"n-logo\" href=\"/\">{E(site.Name)}</a><div class=\"n-wx n-ui\">{Icon("weather")}<span><b>{temp}°F</b> {sky}<br>{E(world.City)} · H {temp + 6}° L {temp - 9}°</span></div></div></header>" +
               $"<nav class=\"n-nav n-ui\"><div class=\"wrap\">{Nav(site, active)}</div></nav>";
    }

    private string SportsHeader(Site site, string active)
    {
        var html = new StringBuilder($"<header class=\"s-head\"><div class=\"wrap\"><a class=\"s-logo\" href=\"/\">{E(site.Name)}</a><nav class=\"s-nav\">{Nav(site, active)}</nav><div class=\"s-icons\">{Icon("search")}{Icon("user")}</div></div></header>");
        html.Append("<div class=\"s-ticker\"><div class=\"s-tick\">");
        foreach (var game in site.Games)
            html.Append(GameBox(game, "s-game"));
        return html.Append("</div></div>").ToString();
    }

    private static string EntertainmentHeader(Site site, string active)
    {
        var html = new StringBuilder($"<header class=\"e-head\"><div class=\"wrap\"><a class=\"e-logo\" href=\"/\">{E(site.Name)}</a><span class=\"e-tagline\">{E(site.Tagline)}</span><div class=\"e-icons\">{Icon("search")}</div></div></header>");
        html.Append($"<nav class=\"e-nav\"><div class=\"wrap\">{string.Concat(site.Sections.Select(s => $"<a href=\"/{Slug(s)}\"{(s == active ? " class=\"on\"" : "")}>{E(s)}</a>"))}</div></nav>");
        html.Append("<div class=\"e-trend\"><div class=\"wrap\"><b>Trending</b>");
        foreach (var item in site.Items.Take(5))
        {
            var f = item.Facts;
            var match = Regex.Match(item.Headline, @"(?:^|\s)'([^']+)'");
            var quoted = match.Success ? match.Groups[1].Value : null;
            var label = item.Section == "Music" ? f.Artist : item.Section == "Celebrity" ? f.Star : quoted ?? (item.Headline.Contains(f.Star) ? f.Star : item.Section);
            html.Append($"<a href=\"/article/{item.Id}\">{E(label)}</a>");
        }
        return html.Append("</div></div>").ToString();
    }

    private string ShopHeader(Site site, string active)
    {
        var html = new StringBuilder("<header class=\"h-head\"><div class=\"h-row\">");
        html.Append($"<a class=\"h-logo\" href=\"/\">{Icon("bolt")}<span>{E(site.Name)}</span></a>");
        html.Append($"<div class=\"h-loc hide-sm\">{Icon("pin")}<span><small>Deliver to</small><b>{E(world.City)} {Zip}</b></span></div>");
        html.Append("<form class=\"h-search\" action=\"/s\" method=\"get\" role=\"search\"><select name=\"i\" aria-label=\"Department\"><option>All</option>");
        foreach (var section in site.Sections)
            html.Append($"<option>{E(section)}</option>");
        html.Append($"</select><input name=\"q\" type=\"search\" placeholder=\"Search {E(site.Name)}\" aria-label=\"Search\"><button type=\"submit\" aria-label=\"Go\">{Icon("search")}</button></form>");
        html.Append("<div class=\"h-acct hide-sm\"><small>Hello, sign in</small><b>Account &amp; Lists</b></div><div class=\"h-acct hide-sm\"><small>Returns</small><b>&amp; Orders</b></div>");
        html.Append($"<a class=\"h-cart\" href=\"/cart\">{Icon("cart")}<span class=\"h-count\">0</span><b>Cart</b></a></div></header>");
        html.Append($"<nav class=\"h-nav\"><div class=\"h-row\"><span class=\"h-all\">{Icon("menu")} All</span><a href=\"/\">Today's Deals</a>{Nav(site, active)}<a href=\"/contact\">Customer Service</a></div></nav>");
        return html.ToString();
    }

    private string Footer(Site site)
    {
        var sections = string.Concat(site.Sections.Select(s => $"<a href=\"/{Slug(s)}\">{E(s)}</a>"));
        var year = world.ReferenceDate.Year;
        return site.Type switch
        {
            "shopping" =>
                $"<footer class=\"h-foot\"><a class=\"h-top\" href=\"#top\">Back to top</a><div class=\"h-fcols\"><div><h4>Get to Know Us</h4><a href=\"/about\">About {E(site.Name)}</a><a href=\"/contact\">Contact Us</a></div>" +
                $"<div><h4>Shop by Department</h4>{sections}</div><div><h4>Let Us Help You</h4><a href=\"/cart\">Your Cart</a><a href=\"/contact\">Shipping Rates &amp; Policies</a><a href=\"/contact\">Returns &amp; Replacements</a><a href=\"/contact\">Help</a></div>" +
                $"<div><h4>Policies</h4><a href=\"/privacy\">Privacy Notice</a><a href=\"/terms\">Conditions of Use</a></div></div>" +
                $"<div class=\"h-fbot\"><a class=\"h-logo\" href=\"/\">{Icon("bolt")}<span>{E(site.Name)}</span></a><p><a href=\"/terms\">Conditions of Use</a> <a href=\"/privacy\">Privacy Notice</a> &copy; {year - 12}-{year}, {E(site.Name)}, Inc. or its affiliates</p></div></footer>",
            "sports" =>
                $"<footer class=\"s-foot\"><div class=\"wrap\"><a class=\"s-logo\" href=\"/\">{E(site.Name)}</a><nav>{sections}</nav><nav><a href=\"/about\">About</a><a href=\"/contact\">Contact</a><a href=\"/privacy\">Privacy Policy</a><a href=\"/terms\">Terms of Use</a></nav>" +
                $"<p>&copy; {year} {E(site.Name)}. All rights reserved.</p></div></footer>",
            "entertainment" =>
                $"<footer class=\"e-foot\"><div class=\"wrap\"><a class=\"e-logo\" href=\"/\">{E(site.Name)}</a><nav>{sections}</nav><nav><a href=\"/about\">About Us</a><a href=\"/contact\">Contact</a><a href=\"/privacy\">Privacy Policy</a><a href=\"/terms\">Terms of Use</a></nav>" +
                $"<p>&copy; {year} {E(site.Name)} Media. All rights reserved.</p></div></footer>",
            _ =>
                $"<footer class=\"n-foot n-ui\"><div class=\"wrap\"><a class=\"n-logo\" href=\"/\">{E(site.Name)}</a><div class=\"n-fcols\"><div><h4>News</h4>{string.Concat(site.Sections.Take(6).Select(s => $"<a href=\"/{Slug(s)}\">{E(s)}</a>"))}</div>" +
                $"<div><h4>More</h4>{string.Concat(site.Sections.Skip(6).Select(s => $"<a href=\"/{Slug(s)}\">{E(s)}</a>"))}</div><div><h4>Company</h4><a href=\"/about\">About Us</a><a href=\"/contact\">Contact Us</a><a href=\"/privacy\">Privacy Policy</a><a href=\"/terms\">Terms of Service</a></div>" +
                $"<div><h4>Subscribe</h4><span>Home Delivery</span><span>Digital Subscriptions</span><span>Newsletters</span><span>Gift Subscriptions</span></div></div>" +
                $"<p class=\"n-copy\">&copy; {year} {E(site.Name)}. All rights reserved.</p></div></footer>"
        };
    }

    private static string Img(Item item) => $"<img src=\"{item.Image}\" alt=\"{E(item.Headline)}\">";

    // a photo, or for sports stories without one a broadcast-style team graphic
    private static string Media(Item item)
    {
        if (item.Image.StartsWith('/'))
            return Img(item);
        var f = item.Facts;
        if (item.Image == "match")
        {
            var middle = item.Kind == "recap" ? $"{f.Score}<i>-</i>{f.OtherScore}<small>Final</small>" : "VS<small>This week</small>";
            return $"<span class=\"s-art\" role=\"img\" aria-label=\"{E(item.Headline)}\" style=\"background:linear-gradient(105deg,{f.Team.Primary} 0 50%,{f.Other.Primary} 50% 100%)\">{Crest(f.Team, "s-big")}<span class=\"s-art-score\">{middle}</span>{Crest(f.Other, "s-big")}</span>";
        }
        return $"<span class=\"s-art solo\" role=\"img\" aria-label=\"{E(item.Headline)}\" style=\"background-color:{f.Team.Primary}\">{Crest(f.Team, "s-big")}<span class=\"s-art-name\">{E(f.Team.Full)}</span></span>";
    }

    private static string Crest(Team team, string cls = "s-crest") =>
        $"<svg class=\"{cls}\" viewBox=\"0 0 40 46\" aria-hidden=\"true\"><path d=\"M20 2 37 8v14c0 11-7.5 18.5-17 22C10.5 40.5 3 33 3 22V8z\" fill=\"{team.Primary}\" stroke=\"{team.Secondary}\" stroke-width=\"3\"/>" +
        $"<text x=\"20\" y=\"28\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-weight=\"800\" font-size=\"12\" fill=\"#fff\">{team.Abbr}</text></svg>";

    private static string Avatar(Writer writer, string cls = "ava") => writer.Photo == null
        ? $"<span class=\"{cls}\" aria-hidden=\"true\">{writer.Initials}</span>"
        : $"<img class=\"{cls}\" src=\"{writer.Photo}\" alt=\"{E(writer.Name)}\">";

    private static string Icon(string name) => name switch
    {
        "search" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" aria-hidden=\"true\"><circle cx=\"10.5\" cy=\"10.5\" r=\"6.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\"/><path d=\"M15.5 15.5 21 21\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\"/></svg>",
        "user" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"8\" r=\"4\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/><path d=\"M4 21c1.5-4 4.5-6 8-6s6.5 2 8 6\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/></svg>",
        "cart" => "<svg viewBox=\"0 0 34 28\" width=\"38\" height=\"30\" aria-hidden=\"true\"><path d=\"M1 3h5l4 15h17l4-11H8\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linejoin=\"round\"/><circle cx=\"12\" cy=\"24\" r=\"2.4\" fill=\"currentColor\"/><circle cx=\"25\" cy=\"24\" r=\"2.4\" fill=\"currentColor\"/></svg>",
        "menu" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" aria-hidden=\"true\"><path d=\"M3 6h18M3 12h18M3 18h18\" stroke=\"currentColor\" stroke-width=\"2.4\"/></svg>",
        "pin" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" aria-hidden=\"true\"><path d=\"M12 22s7-7.5 7-13a7 7 0 0 0-14 0c0 5.5 7 13 7 13z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/><circle cx=\"12\" cy=\"9\" r=\"2.5\" fill=\"currentColor\"/></svg>",
        "weather" => "<svg viewBox=\"0 0 32 24\" width=\"32\" height=\"24\" aria-hidden=\"true\"><circle cx=\"11\" cy=\"9\" r=\"5\" fill=\"#f5b400\"/><path d=\"M9 21h16a5 5 0 0 0 0-10 7 7 0 0 0-13 1 4.5 4.5 0 0 0-3 9z\" fill=\"#c9ced6\"/></svg>",
        "bolt" => "<svg viewBox=\"0 0 40 40\" width=\"34\" height=\"34\" aria-hidden=\"true\"><path d=\"M8.5 30A15 15 0 0 1 12 7.5M31.5 10A15 15 0 0 1 28 32.5\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3\" stroke-linecap=\"round\"/><path d=\"M23 3 11 22h8l-3 15 13-20h-8z\" fill=\"#f0592a\"/></svg>",
        "share" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" aria-hidden=\"true\"><circle cx=\"18\" cy=\"5\" r=\"2.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/><circle cx=\"6\" cy=\"12\" r=\"2.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/><circle cx=\"18\" cy=\"19\" r=\"2.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/><path d=\"m8.2 10.8 7.6-4.4M8.2 13.2l7.6 4.4\" stroke=\"currentColor\" stroke-width=\"1.8\"/></svg>",
        "mail" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" aria-hidden=\"true\"><rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/><path d=\"m3.5 6 8.5 7 8.5-7\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/></svg>",
        "bookmark" => "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" aria-hidden=\"true\"><path d=\"M6 3h12v18l-6-4.5L6 21z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\"/></svg>",
        "camera" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" aria-hidden=\"true\"><path d=\"M4 7h3l2-3h6l2 3h3v13H4z\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linejoin=\"round\"/><circle cx=\"12\" cy=\"13\" r=\"3.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"/></svg>",
        _ => ""
    };

    private const string BaseCss = """
        *{margin:0;padding:0;box-sizing:border-box}
        html{-webkit-text-size-adjust:100%}
        body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Helvetica,Arial,sans-serif;color:#121212;background:#fff;line-height:1.4}
        a{color:inherit;text-decoration:none}
        img{display:block;max-width:100%}
        button,input,select{font:inherit}
        ul,ol{list-style:none}
        .wrap{max-width:1240px;margin:0 auto;padding:0 20px}
        .ratio{display:block;position:relative;aspect-ratio:3/2;overflow:hidden;background:#e9e9e9}
        .ratio.r169{aspect-ratio:16/9}
        .ratio>img,.ratio>.s-art{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .info{max-width:760px;margin:40px auto 60px;padding:0 20px;line-height:1.7}
        .info h1{font-size:36px;line-height:1.15;margin-bottom:14px}
        .info h2{font-size:20px;margin:28px 0 6px}
        .info p,.info li{margin:8px 0;color:#333;font-size:17px}
        .info ul{list-style:disc;padding-left:22px}
        .info .updated{color:#666;font-size:14px}
        .info-link{text-decoration:underline}
        .ava{width:44px;height:44px;border-radius:50%;object-fit:cover;background:#e3ddd2;color:#5a5348;display:inline-flex;align-items:center;justify-content:center;font:700 15px Georgia,serif;flex:none}
        .a-wrap{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:48px;margin-top:28px;margin-bottom:40px}
        .a-main{max-width:720px}
        .a-kicker{display:flex;gap:10px;font:700 12px Helvetica,Arial,sans-serif;letter-spacing:.06em;text-transform:uppercase;color:var(--accent)}
        .a-kicker span{color:#555}
        .a-main h1{font-size:42px;line-height:1.08;margin:10px 0 14px;letter-spacing:-.5px}
        .a-dek{font-size:20px;line-height:1.4;color:#363636;margin-bottom:20px}
        .a-by{display:flex;align-items:center;gap:12px;font:14px Helvetica,Arial,sans-serif;border-top:1px solid #e2e2e2;border-bottom:1px solid #e2e2e2;padding:12px 0;margin-bottom:22px}
        .a-by a{font-weight:700;text-decoration:underline;text-underline-offset:2px}
        .a-by time{display:block;color:#666;font-size:12px;margin-top:3px}
        .a-share{margin-left:auto;display:flex;gap:8px;color:#333}
        .a-share svg{width:34px;height:34px;padding:8px;border:1px solid #ddd;border-radius:50%}
        .a-fig{margin-bottom:26px}
        .a-fig figcaption{font:13px/1.4 Helvetica,Arial,sans-serif;color:#555;margin-top:8px}
        .a-fig figcaption span{color:#888;font-size:11px;text-transform:uppercase;letter-spacing:.04em}
        .a-body p{font-size:19px;line-height:1.65;margin:0 0 20px;color:#222}
        .a-body h2{font-size:24px;line-height:1.25;margin:32px 0 12px}
        .a-rel{font-family:Helvetica,Arial,sans-serif;font-size:16px!important;border-left:4px solid var(--accent);padding-left:12px}
        .a-rel a{text-decoration:underline}
        .a-bio{display:flex;gap:14px;align-items:flex-start;border-top:1px solid #e2e2e2;padding-top:18px;margin-top:8px;font:15px/1.5 Helvetica,Arial,sans-serif;color:#444}
        .a-bio a{font-weight:700;color:#121212}
        .a-more{margin-top:36px;border-top:2px solid #121212;padding-top:12px}
        .a-more h2{font:700 15px Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em;margin-bottom:14px}
        .a-g3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}
        .a-g3 h3{font-size:16px;line-height:1.25;margin-top:6px}
        .a-mk{font:700 11px Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em;color:var(--accent);margin-top:8px}
        .river{display:grid;grid-template-columns:80px minmax(0,1fr) 200px;gap:20px;padding:20px 0;border-bottom:1px solid #e2e2e2}
        .river time{font:12px Helvetica,Arial,sans-serif;color:#666;padding-top:3px}
        .river h3{font-size:21px;line-height:1.2;margin:4px 0 6px}
        .river p{font-size:15px;color:#444;line-height:1.45}
        .r-kicker{font:700 11px Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em;color:var(--accent)}
        .r-meta{font:12px Helvetica,Arial,sans-serif;color:#666;margin-top:8px}
        .au{margin-top:30px;margin-bottom:50px;max-width:980px}
        .au-head{display:flex;gap:26px;align-items:flex-start;padding-bottom:26px;border-bottom:1px solid #e2e2e2}
        .au-ava{width:120px;height:120px;border-radius:50%;object-fit:cover;background:#e3ddd2;color:#5a5348;display:inline-flex;align-items:center;justify-content:center;font:700 40px Georgia,serif;flex:none}
        .au-head h1{font-size:38px;line-height:1.1}
        .au-head p{font-size:17px;line-height:1.55;color:#333;margin-top:8px;max-width:640px}
        .au-role{font:700 13px Helvetica,Arial,sans-serif!important;text-transform:uppercase;letter-spacing:.05em;color:var(--accent)!important}
        .au-mail{display:flex;align-items:center;gap:8px;font:14px Helvetica,Arial,sans-serif!important}
        .au-h{font:700 14px Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em;margin-top:26px}
        @media(max-width:900px){.a-wrap{grid-template-columns:1fr;gap:24px}.a-main{max-width:none}}
        @media(max-width:600px){
          .wrap{padding:0 16px}
          .a-main h1{font-size:30px}.a-dek{font-size:18px}.a-body p{font-size:18px}
          .a-share{display:none}.a-g3{grid-template-columns:1fr;gap:14px}
          .a-g3 article{display:grid;grid-template-columns:minmax(0,1fr) 120px;gap:12px;padding-bottom:14px;border-bottom:1px solid #e2e2e2}.a-g3 article .ratio{order:2}.a-mk{margin-top:0}
          .river{grid-template-columns:minmax(0,1fr) 110px;gap:14px}.river time{display:none}.river h3{font-size:18px}.river p{display:none}
          .au-head{flex-direction:column;gap:14px}.au-ava{width:88px;height:88px;font-size:30px}.au-head h1{font-size:30px}
          .info h1{font-size:30px}
        }
        """;

    private const string NewsCss = """
        .news{font-family:Georgia,"Times New Roman",Times,serif}
        .n-ui{font-family:"Helvetica Neue",Helvetica,Arial,sans-serif}
        .n-topbar{border-bottom:1px solid #e2e2e2;font-size:12px}
        .n-topbar .wrap{display:flex;justify-content:space-between;align-items:center;min-height:42px;gap:12px}
        .n-ed span{margin-right:16px;color:#333}.n-ed span:first-child{font-weight:700}
        .n-acct{display:flex;gap:8px;margin-left:auto}
        .n-btn{background:#326891;color:#fff;border:0;border-radius:3px;padding:8px 12px;font-size:11px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;cursor:pointer}
        .n-btn.alt{background:#fff;color:#121212;border:1px solid #ccc}
        .n-mast .wrap{display:grid;grid-template-columns:1fr auto 1fr;align-items:end;gap:16px;padding-top:18px;padding-bottom:12px}
        .n-date{font-size:13px;font-weight:700}.n-date small{display:block;font-weight:400;color:#555;margin-top:2px;font-size:12px}
        .n-logo{font:700 60px/1 Georgia,"Times New Roman",serif;letter-spacing:-1.5px;text-align:center;white-space:nowrap}
        .n-wx{justify-self:end;display:flex;align-items:center;gap:8px;font-size:12px;color:#333;line-height:1.35}
        .n-nav{border-top:1px solid #e2e2e2;border-bottom:1px solid #121212;box-shadow:0 3px 0 -1px #fff,0 4px 0 -1px #121212}
        .n-nav .wrap{display:flex;justify-content:center;gap:6px 24px;flex-wrap:wrap;padding-top:11px;padding-bottom:11px;font-size:13px}
        .n-nav a:hover{text-decoration:underline}.n-nav a.on{font-weight:700}
        .n-alert{margin-top:14px}
        .n-alert .wrap{display:flex;align-items:center;gap:12px;font-size:14px}
        .n-alert b{background:var(--accent);color:#fff;font-size:11px;text-transform:uppercase;letter-spacing:.06em;padding:3px 7px;border-radius:2px}
        .n-alert a{font-weight:700}.n-alert a:hover{text-decoration:underline}
        .n-alert span{color:#777;font-size:12px;white-space:nowrap}
        .n-top{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:30px;margin-top:18px}
        .n-pkg{display:grid;grid-template-columns:minmax(0,1.6fr) minmax(0,1fr);gap:26px;padding-right:30px;border-right:1px solid #e2e2e2}
        .n-kicker{font:700 11px "Helvetica Neue",Helvetica,Arial,sans-serif;letter-spacing:.06em;text-transform:uppercase;color:var(--accent);margin-top:12px}
        .n-lead h2{font-size:36px;line-height:1.1;font-weight:700;margin:6px 0 10px;letter-spacing:-.4px}
        .n-lead h2 a:hover,.n-story h3 a:hover,.river h3 a:hover,.n-sectop h2 a:hover{color:#555}
        .n-dek{font-size:17px;line-height:1.45;color:#363636}
        .n-bul{margin-top:14px;border-top:1px solid #e2e2e2}
        .n-bul li{padding:10px 0 10px 16px;border-bottom:1px solid #e2e2e2;font-size:15px;font-weight:700;line-height:1.3;position:relative}
        .n-bul li::before{content:"";position:absolute;left:0;top:17px;width:6px;height:6px;background:#121212;border-radius:50%}
        .n-meta{font:11px "Helvetica Neue",Helvetica,Arial,sans-serif;color:#666;letter-spacing:.03em;text-transform:uppercase;margin-top:10px}
        .n-sec .n-story{padding-bottom:16px;margin-bottom:16px;border-bottom:1px solid #e2e2e2}
        .n-sec .n-story:last-child{border-bottom:0}
        .n-story h3{font-size:20px;line-height:1.2;margin:6px 0 6px}
        .n-story .ratio+h3{margin-top:10px}
        .n-story p{font-size:14px;color:#363636;line-height:1.45}
        .n-rail h2{font:700 13px "Helvetica Neue",Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.05em;border-top:2px solid #121212;padding-top:8px;margin-bottom:6px}
        .n-rail section{margin-bottom:28px}
        .n-latest li{display:grid;grid-template-columns:66px 1fr;gap:8px;padding:9px 0;border-bottom:1px solid #eee;font-size:15px;line-height:1.3}
        .n-latest time{font:700 11px "Helvetica Neue",Helvetica,Arial,sans-serif;color:var(--accent);padding-top:3px}
        .n-latest a:hover,.n-mr a:hover,.n-op a:hover h3{text-decoration:underline}
        .n-op a{display:grid;grid-template-columns:1fr 60px;gap:12px;align-items:center;padding:11px 0;border-bottom:1px solid #eee}
        .n-op .who{display:block;font:700 11px "Helvetica Neue",Helvetica,Arial,sans-serif;text-transform:uppercase;letter-spacing:.04em;color:#555;margin-bottom:4px}
        .n-op h3{font-size:17px;line-height:1.25;font-style:italic}
        .n-op .ava{width:60px;height:60px}
        .n-mr{counter-reset:mr}
        .n-mr li{display:grid;grid-template-columns:32px 1fr;padding:10px 0;border-bottom:1px solid #eee;font-size:16px;line-height:1.3;font-weight:700}
        .n-mr li::before{content:counter(mr);counter-increment:mr;font:700 24px/1 Georgia,serif;color:var(--accent)}
        .n-block{margin-top:38px;border-top:1px solid #121212;padding-top:10px}
        .n-block>h2{font-size:15px;font-weight:700;margin-bottom:14px}.n-block>h2 a::after{content:" ›"}.n-block>h2 a:hover{text-decoration:underline}
        .n-g4{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:24px}
        .n-g4 h3{font-size:18px}
        .n-blocks{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);column-gap:30px;grid-auto-flow:row dense}
        .n-block{grid-column:1/-1}.n-block.half{grid-column:auto}
        .n-g4.n-c3{grid-template-columns:repeat(3,minmax(0,1fr))}.n-g4.n-c2{grid-template-columns:repeat(2,minmax(0,1fr))}.n-g4.n-c1{grid-template-columns:minmax(0,1fr)}
        .n-block.half .n-g4{grid-template-columns:repeat(2,minmax(0,1fr))}
        .n-block.half .n-c1{grid-template-columns:1fr}.n-c1 .n-story{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);column-gap:18px}.n-c1 .n-story .ratio{grid-row:span 2}.n-c1 .n-story h3{margin-top:0}
        .n-sechead{border-bottom:1px solid #121212;padding:26px 0 14px}
        .n-sechead h1{font-size:44px;line-height:1}.n-sechead p{color:#555;font-size:14px;margin-top:8px}
        .n-sectop{display:grid;grid-template-columns:minmax(0,1.4fr) minmax(0,1fr);gap:24px;padding-bottom:22px;border-bottom:1px solid #e2e2e2}
        .n-sectop h2{font-size:30px;line-height:1.12;margin-bottom:10px}
        .n-riverh{font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;margin-top:22px}
        .n-foot{border-top:2px solid #121212;margin-top:50px;padding:26px 0 40px}
        .n-foot .n-logo{display:inline-block;font-size:32px;text-align:left}
        .n-fcols{display:grid;grid-template-columns:repeat(4,1fr);gap:20px;margin-top:22px}
        .n-fcols h4{font-size:12px;text-transform:uppercase;letter-spacing:.05em;margin-bottom:8px}
        .n-fcols a,.n-fcols span{display:block;font-size:14px;color:#333;padding:3px 0}
        .n-fcols a:hover{text-decoration:underline}
        .n-copy{font-size:12px;color:#666;margin-top:26px;border-top:1px solid #e2e2e2;padding-top:14px}
        .a-news .a-main h1{font-size:44px}
        @media(max-width:1000px){.n-top{grid-template-columns:1fr}.n-pkg{padding-right:0;border-right:0}.n-g4{grid-template-columns:repeat(2,minmax(0,1fr))}}
        @media(max-width:700px){
          .n-ed,.n-btn.alt{display:none}
          .n-mast .wrap{grid-template-columns:1fr;justify-items:center;text-align:center;gap:6px;padding-top:12px}
          .n-logo{font-size:36px;order:-1}.n-wx{justify-self:center}.n-date small{display:none}
          .n-nav .wrap{flex-wrap:nowrap;overflow-x:auto;justify-content:flex-start;white-space:nowrap;gap:20px}
          .n-pkg{grid-template-columns:1fr}.n-lead h2{font-size:27px}
          .n-blocks{grid-template-columns:1fr}.n-blocks .n-g4,.n-block.half .n-g4{grid-template-columns:1fr}.n-c1 .n-story .ratio{grid-row:auto}
          .n-g4{grid-template-columns:1fr}.n-g4 .n-story{display:grid;grid-template-columns:minmax(0,1fr) 120px;gap:12px}.n-g4 .ratio{order:2}.n-g4 h3{margin-top:0;font-size:17px}.n-g4 p{display:none}
          .n-sectop{grid-template-columns:1fr}.n-sectop h2{font-size:25px}.n-sechead h1{font-size:34px}
          .n-fcols{grid-template-columns:1fr 1fr}
          .a-news .a-main h1{font-size:31px}
        }
        """;

    private const string SportsCss = """
        .sports{background:#edeef0;font-family:Helvetica,Arial,sans-serif;color:#121213}
        .s-head{background:#121213;color:#fff;border-bottom:3px solid var(--accent)}
        .s-head .wrap{display:flex;align-items:center;gap:28px;height:58px}
        .s-logo{font:italic 900 28px/1 "Arial Black",Arial,sans-serif;letter-spacing:-1px;color:var(--accent);text-transform:uppercase;white-space:nowrap}
        .s-nav{display:flex;gap:22px;font-size:14px;font-weight:700;overflow-x:auto;white-space:nowrap;scrollbar-width:none}
        .s-nav a{color:#fff;padding:19px 0 16px;border-bottom:3px solid transparent}
        .s-nav a:hover,.s-nav a.on{border-bottom-color:#fff}
        .s-icons{margin-left:auto;display:flex;gap:18px;color:#fff}
        .s-ticker{background:#fff;border-bottom:1px solid #d8d8d8;box-shadow:0 1px 2px rgba(0,0,0,.05)}
        .s-tick{display:flex;overflow-x:auto;max-width:1240px;margin:0 auto;scrollbar-width:thin}
        .s-game{flex:none;width:136px;padding:8px 12px 9px;border-right:1px solid #e6e6e6;font-size:13px;position:relative}
        a.s-game:hover{background:#f5f6f7}
        .s-g-top{display:flex;justify-content:space-between;font-size:10px;font-weight:700;color:#6c6d6f;text-transform:uppercase;letter-spacing:.03em;margin-bottom:5px}
        .s-g-row{display:flex;align-items:center;gap:6px;padding:2px 0;color:#6c6d6f;font-weight:700}
        .s-g-row b{margin-left:auto}
        .s-g-row.win{color:#121213}
        .s-crest{width:17px;height:19px;flex:none}
        .s-g-recap{display:none}
        .s-main{max-width:1240px;margin:16px auto 30px;padding:0 16px;display:grid;grid-template-columns:220px minmax(0,1fr) 300px;gap:16px;align-items:start}
        .s-main.s-two{grid-template-columns:minmax(0,1fr) 320px}
        .s-card{background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.12);overflow:hidden;margin-bottom:16px}
        .s-ch{font-size:14px;font-weight:800;padding:12px 14px;border-bottom:1px solid #e6e6e6}
        .s-heads li{border-bottom:1px solid #f0f0f0}
        .s-heads a{display:block;padding:10px 14px;font-size:13px;font-weight:700;line-height:1.3}
        .s-heads a:hover,.s-mr a:hover,.s-story h3 a:hover,.s-lead h1 a:hover{text-decoration:underline}
        .s-links a{font-weight:400}
        .s-lead>a{display:block;aspect-ratio:16/9;position:relative;overflow:hidden;background:#222}
        .s-lead>a>img,.s-lead>a>.s-art{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .s-lead-text{padding:16px 18px 18px}
        .s-lead h1{font-size:30px;line-height:1.12;font-weight:900;letter-spacing:-.4px;margin:6px 0 8px}
        .s-lead p{color:#48494a;font-size:15px}
        .s-label{font-size:11px;font-weight:800;text-transform:uppercase;letter-spacing:.06em;color:var(--accent)}
        .s-story{display:grid;grid-template-columns:250px minmax(0,1fr);gap:16px;padding:14px;border-top:4px solid #121213}
        .s-thumb{display:block;aspect-ratio:16/9;position:relative;overflow:hidden;border-radius:4px;background:#222}
        .s-thumb>img,.s-thumb>.s-art{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .s-story h3{font-size:19px;font-weight:800;line-height:1.2;margin:4px 0 6px}
        .s-story p{font-size:14px;color:#48494a;line-height:1.4}
        .s-meta{font-size:11px;color:#6c6d6f;margin-top:8px}
        .s-art{display:flex;align-items:center;justify-content:space-around;color:#fff;overflow:hidden}
        .s-art .s-big{width:24%;height:auto;filter:drop-shadow(0 4px 8px rgba(0,0,0,.35))}
        .s-art-score{font:900 clamp(16px,4.2vw,46px)/1 "Arial Black",Arial,sans-serif;text-align:center;text-shadow:0 2px 8px rgba(0,0,0,.45);font-style:italic}
        .s-art-score i{font-style:normal;margin:0 .15em;opacity:.7}
        .s-art-score small{display:block;font:800 .3em/1 Arial,sans-serif;letter-spacing:.14em;text-transform:uppercase;margin-top:.6em;font-style:normal}
        .s-art.solo{flex-direction:column;justify-content:center;gap:4%;background-image:repeating-linear-gradient(135deg,rgba(255,255,255,.07) 0 14px,transparent 14px 28px),linear-gradient(160deg,rgba(255,255,255,.15),rgba(0,0,0,.35))}
        .s-art.solo .s-big{width:22%}
        .s-art-name{font:italic 900 clamp(12px,2.4vw,24px)/1 "Arial Black",Arial,sans-serif;text-transform:uppercase;letter-spacing:-.3px;text-shadow:0 2px 6px rgba(0,0,0,.4)}
        .s-table{width:100%;border-collapse:collapse;font-size:13px}
        .s-table th{font-size:11px;color:#6c6d6f;text-align:right;padding:7px 10px;border-bottom:1px solid #e6e6e6;font-weight:700}
        .s-table td{padding:7px 10px;border-bottom:1px solid #f0f0f0;text-align:right;font-variant-numeric:tabular-nums}
        .s-table td:first-child,.s-table th:first-child{text-align:left}
        .s-tm{display:flex;align-items:center;gap:8px;font-weight:700;white-space:nowrap}
        .s-rk{width:16px;color:#6c6d6f;font-weight:400}
        .s-mr{counter-reset:m}
        .s-mr li{display:grid;grid-template-columns:28px 1fr;padding:10px 14px;border-bottom:1px solid #f0f0f0;font-size:13px;font-weight:700;line-height:1.3}
        .s-mr li::before{content:counter(m);counter-increment:m;color:var(--accent);font-weight:900}
        .s-sechead{background:#fff;border-bottom:1px solid #d8d8d8}
        .s-sechead .wrap{display:flex;align-items:flex-end;gap:30px;padding-top:18px}
        .s-sechead h1{font-size:30px;font-weight:900;padding-bottom:12px}
        .s-sechead nav{display:flex;gap:22px;font-size:14px;font-weight:700}
        .s-sechead nav a{padding-bottom:12px;border-bottom:3px solid transparent}.s-sechead nav a:first-child{border-bottom-color:var(--accent)}
        .s-board{display:grid;grid-template-columns:repeat(auto-fill,minmax(170px,1fr))}
        .s-bgame{padding:12px 14px;border-right:1px solid #f0f0f0;border-bottom:1px solid #f0f0f0;font-size:14px}
        a.s-bgame:hover{background:#f5f6f7}
        .s-bgame .s-g-recap{display:inline-block;margin-top:6px;font-size:11px;font-weight:700;color:#06c}
        .s-box{background:#fff;border:1px solid #e6e6e6;border-radius:6px;margin:0 0 24px;overflow-x:auto}
        .a-sports .a-main{background:#fff;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.12);padding:24px 28px;max-width:none}
        .a-sports .a-main h1{font-weight:900;font-family:Helvetica,Arial,sans-serif;font-size:38px}
        .a-sports .a-body p{font-family:Georgia,serif;font-size:18px}
        .a-sports .a-body h2{font-family:Helvetica,Arial,sans-serif;font-weight:800;font-size:21px}
        .a-sports .a-more{border-top-color:var(--accent)}
        .a-sports.a-wrap{max-width:1240px;margin-top:16px}
        .au.a-sports{background:#fff;border-radius:6px;padding:26px;margin-top:16px}
        .sports .info{background:#fff;border-radius:6px;padding:30px;margin-top:20px}
        .s-foot{background:#121213;color:#aaa;margin-top:30px;padding:30px 0}
        .s-foot nav{display:flex;flex-wrap:wrap;gap:8px 22px;margin-top:16px;font-size:13px}
        .s-foot a:hover{color:#fff}.s-foot p{font-size:12px;margin-top:18px}
        @media(max-width:1060px){.s-main{grid-template-columns:minmax(0,1fr) 300px}.s-left{display:none}}
        @media(max-width:800px){
          .s-main,.s-main.s-two{grid-template-columns:1fr;padding:0 10px}
          .s-head .wrap{flex-wrap:wrap;height:auto;gap:0 14px;padding-top:12px}
          .s-logo{font-size:24px}.s-icons{order:2}
          .s-nav{order:3;width:100%;gap:18px}.s-nav a{padding:12px 0 10px}
          .s-story{grid-template-columns:minmax(0,1fr) 120px;gap:12px}.s-story .s-thumb{order:2}.s-story p{display:none}.s-story h3{font-size:16px}
          .s-lead h1{font-size:24px}
          .a-sports .a-main{padding:18px 16px}.a-sports .a-main h1{font-size:28px}
          .s-sechead h1{font-size:24px}
        }
        """;

    private const string EntertainmentCss = """
        .entertainment{font-family:Helvetica,Arial,sans-serif;color:#111}
        .e-head{background:#000;color:#fff}
        .e-head .wrap{display:flex;align-items:center;gap:22px;height:78px}
        .e-logo{font:900 42px/1 "Arial Black",Impact,Arial,sans-serif;letter-spacing:-1.5px;text-transform:uppercase;white-space:nowrap}
        .e-logo::after{content:"";display:inline-block;width:.24em;height:.24em;background:var(--accent);border-radius:50%;margin-left:.08em}
        .e-tagline{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#bbb}
        .e-icons{margin-left:auto}
        .e-nav{border-bottom:1px solid #e5e5e5;background:#fff}
        .e-nav .wrap{display:flex;gap:28px;overflow-x:auto;white-space:nowrap;font-weight:800;font-size:13px;text-transform:uppercase;letter-spacing:.05em;scrollbar-width:none}
        .e-nav a{padding:15px 0 12px;border-bottom:3px solid transparent}
        .e-nav a:hover,.e-nav a.on{border-bottom-color:var(--accent)}
        .e-trend{background:#f4f4f4}
        .e-trend .wrap{display:flex;gap:20px;align-items:center;height:42px;overflow-x:auto;white-space:nowrap;font-size:13px;font-weight:700;scrollbar-width:none}
        .e-trend b{background:var(--accent);color:#fff;padding:4px 9px;font-size:11px;letter-spacing:.08em;text-transform:uppercase}
        .e-trend a:hover{color:var(--accent)}
        .e-hero{display:grid;grid-template-columns:2fr 1fr 1fr;grid-template-rows:270px 270px;gap:6px;margin-top:20px}
        .e-hero a{position:relative;overflow:hidden;display:block;background:#222}
        .e-hero a:first-child{grid-row:span 2}
        .e-hero img{width:100%;height:100%;object-fit:cover;transition:transform .5s}
        .e-hero a:hover img,.e-photos a:hover img{transform:scale(1.04)}
        .e-hero3{grid-template-columns:2fr 1fr}
        .e-ov{position:absolute;left:0;right:0;bottom:0;padding:70px 18px 16px;background:linear-gradient(transparent,rgba(0,0,0,.88));color:#fff}
        .e-ov h2{font-size:20px;line-height:1.15;font-weight:800}
        .e-hero a:first-child h2{font-size:36px;letter-spacing:-.5px}
        .e-cat{display:inline-block;background:var(--accent);color:#fff;font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;padding:3px 7px;margin-bottom:8px}
        .e-cols{display:grid;grid-template-columns:minmax(0,1fr) 320px;gap:40px;margin-top:40px}
        .e-h{font:900 26px/1 "Arial Black",Arial,sans-serif;text-transform:uppercase;letter-spacing:-.5px;border-bottom:5px solid #000;padding-bottom:8px;margin-bottom:20px}
        .e-h a:hover{color:var(--accent)}
        .e-river article{display:grid;grid-template-columns:300px minmax(0,1fr);gap:22px;padding-bottom:22px;margin-bottom:22px;border-bottom:1px solid #e5e5e5}
        .e-river h3{font-size:23px;line-height:1.15;font-weight:800;margin:2px 0 8px;letter-spacing:-.2px}
        .e-river h3 a:hover,.e-g3 h3 a:hover,.e-pop a:hover{color:var(--accent)}
        .e-river p{color:#444;font-size:15px;line-height:1.45}
        .e-meta{font-size:11px;color:#777;margin-top:10px;text-transform:uppercase;letter-spacing:.05em;font-weight:700}
        .e-rail section{margin-bottom:34px}
        .e-pop{counter-reset:p}
        .e-pop li{display:grid;grid-template-columns:34px minmax(0,1fr) 84px;gap:12px;align-items:center;padding:12px 0;border-bottom:1px solid #eee;font-weight:800;font-size:14px;line-height:1.25}
        .e-pop li::before{content:counter(p);counter-increment:p;font:900 30px/1 "Arial Black",Arial,sans-serif;color:var(--accent)}
        .e-pop img{width:84px;height:62px;object-fit:cover}
        .e-nl{background:#000;color:#fff;padding:24px}
        .e-nl h2{font:900 22px/1.1 "Arial Black",Arial,sans-serif;text-transform:uppercase}
        .e-nl p{font-size:14px;color:#ccc;margin:8px 0 14px}
        .e-nl-f{display:flex}.e-nl-f input{flex:1;min-width:0;border:0;padding:10px}.e-nl-f button{background:var(--accent);color:#fff;border:0;padding:0 16px;font-weight:800;text-transform:uppercase;font-size:12px}
        .e-rev{display:flex;gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid #eee;font-weight:800;font-size:14px;line-height:1.25}
        .e-rev b{flex:none;width:46px;height:46px;border-radius:50%;background:var(--accent);color:#fff;display:flex;align-items:center;justify-content:center;font-size:17px}
        .e-sec{margin-top:40px}
        .e-photos{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px}
        .e-photos a{position:relative;aspect-ratio:3/4;overflow:hidden;display:block;background:#222}
        .e-photos img{width:100%;height:100%;object-fit:cover;transition:transform .5s}
        .e-photos h3{font-size:17px;line-height:1.2}
        .e-cam{display:inline-flex;gap:6px;align-items:center;font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;margin-bottom:6px}
        .e-g3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}
        .e-g3 .e-cat{margin-top:10px}.e-g3 h3{font-size:19px;line-height:1.2;font-weight:800}
        .e-sechead h1{font:900 64px/1 "Arial Black",Arial,sans-serif;text-transform:uppercase;letter-spacing:-2px;margin-top:26px;border-bottom:6px solid var(--accent);display:inline-block;padding-bottom:6px}
        .a-entertainment .a-main h1{font-weight:900;font-size:44px;letter-spacing:-1px}
        .a-entertainment .a-kicker a{background:var(--accent);color:#fff;padding:3px 8px}
        .a-entertainment .a-body p{font-size:18px}
        .a-entertainment .a-body h2{font-weight:900;text-transform:uppercase;font-size:20px}
        .e-foot{background:#000;color:#aaa;margin-top:50px;padding:34px 0}
        .e-foot .e-logo{color:#fff;font-size:30px}
        .e-foot nav{display:flex;flex-wrap:wrap;gap:8px 24px;margin-top:16px;font-size:13px;text-transform:uppercase;font-weight:700;letter-spacing:.04em}
        .e-foot a:hover{color:#fff}.e-foot p{font-size:12px;margin-top:20px}
        @media(max-width:1000px){.e-cols{grid-template-columns:1fr}.e-photos{grid-template-columns:repeat(2,minmax(0,1fr))}}
        @media(max-width:700px){
          .e-head .wrap{height:60px}.e-logo{font-size:28px}.e-tagline{display:none}
          .e-hero,.e-hero3{grid-template-columns:1fr 1fr;grid-template-rows:300px 180px 180px}
          .e-hero a:first-child{grid-column:span 2;grid-row:auto}.e-hero a:first-child h2{font-size:25px}
          .e-hero3{grid-template-rows:300px 180px}
          .e-ov{padding:50px 12px 12px}.e-ov h2{font-size:15px}
          .e-river article{grid-template-columns:1fr;gap:10px}.e-river h3{font-size:20px}
          .e-g3{grid-template-columns:1fr}
          .e-h{font-size:22px}.e-sechead h1{font-size:40px}
          .a-entertainment .a-main h1{font-size:30px}
        }
        """;

    private const string ShopCss = """
        .shopping{background:#eaeded;font-family:Arial,Helvetica,sans-serif;color:#0f1111;font-size:14px}
        .h-head{background:#102a43;color:#fff}
        .h-row{max-width:1500px;margin:0 auto;display:flex;align-items:center;gap:12px;padding:8px 14px}
        .h-logo{display:flex;align-items:center;gap:6px;font:700 23px/1 Arial,sans-serif;letter-spacing:-.5px;white-space:nowrap;padding:5px 6px;border:1px solid transparent;border-radius:2px}
        .h-logo:hover,.h-acct:hover,.h-cart:hover,.h-loc:hover{border-color:#fff}
        .h-loc{display:flex;align-items:flex-end;gap:2px;line-height:1.15;white-space:nowrap;padding:5px 6px;border:1px solid transparent}
        .h-loc small,.h-acct small{display:block;font-size:12px;color:#c8d1db}
        .h-loc b,.h-acct b{font-size:14px}
        .h-search{flex:1;display:flex;height:40px;border-radius:6px;overflow:hidden;min-width:0;background:#fff}
        .h-search:focus-within{box-shadow:0 0 0 3px #f0592a}
        .h-search select{background:#e6e6e6;border:0;padding:0 6px;font-size:12px;color:#555;max-width:64px;border-right:1px solid #cdcdcd}
        .h-search input{flex:1;border:0;padding:0 10px;font-size:15px;min-width:0;outline:0}
        .h-search button{width:46px;border:0;background:var(--accent);color:#fff;cursor:pointer;display:flex;align-items:center;justify-content:center}
        .h-acct{line-height:1.15;white-space:nowrap;padding:5px 6px;border:1px solid transparent;cursor:pointer}
        .h-cart{display:flex;align-items:flex-end;position:relative;font-weight:700;padding:5px 6px;border:1px solid transparent}
        .h-count{position:absolute;top:2px;left:21px;color:var(--accent);font-weight:700;font-size:15px}
        .h-nav{background:#1f3a56;color:#fff}
        .h-nav .h-row{gap:20px;padding:8px 14px;font-size:14px;overflow-x:auto;white-space:nowrap;scrollbar-width:none}
        .h-nav a:hover{text-decoration:underline}
        .h-all{display:flex;align-items:center;gap:4px;font-weight:700}
        .h-page{max-width:1500px;margin:0 auto;padding:0 14px 30px}
        .h-hero{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.25fr);background:linear-gradient(120deg,#fde7dc,#f9d3c0);margin-top:14px;min-height:340px;overflow:hidden}
        .h-hero-text{padding:40px 52px;display:flex;flex-direction:column;justify-content:center;gap:14px}
        .h-hero h1{font-size:44px;line-height:1.03;letter-spacing:-1px}
        .h-hero p{font-size:16px;color:#333;max-width:440px}
        .h-hero-img{position:relative;min-height:240px}.h-hero-img img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:center 30%}
        .h-tag{align-self:flex-start;background:#cc0c39;color:#fff;font-size:12px;font-weight:700;padding:4px 8px;border-radius:2px}
        .h-btn{align-self:flex-start;display:inline-block;background:#ffd33d;color:#0f1111;padding:11px 26px;border-radius:22px;font-weight:700;border:1px solid #e6b800}
        .h-btn:hover{background:#f7ca00}
        .h-tiles{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px;margin-top:18px}
        .h-tile{background:#fff;padding:18px 18px 14px;display:flex;flex-direction:column}
        .h-tile h2{font-size:20px;margin-bottom:12px}
        .h-quad{display:grid;grid-template-columns:1fr 1fr;gap:12px 14px;flex:1}
        .h-quad a{display:block;font-size:12px;color:#333}
        .h-quad img{width:100%;aspect-ratio:1;object-fit:contain;background:#f7f7f7;padding:10px;margin-bottom:4px}
        .h-solo{display:block;flex:1;min-height:200px;position:relative;overflow:hidden}.h-solo img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .h-more,.h-sec h2 a,.h-rc,.h-crumbs a:hover,.h-brand,.h-filters a{color:#007185}
        .h-more{font-size:13px;margin-top:12px}.h-more:hover,.h-sec h2 a:hover{text-decoration:underline;color:#c7511f}
        .h-sec{background:#fff;padding:18px 20px;margin-top:18px}
        .h-sec h2{font-size:21px;margin-bottom:14px;display:flex;flex-wrap:wrap;gap:4px 16px;align-items:baseline}
        .h-sec h2 a{font-size:13px;font-weight:400}
        .h-strip{display:grid;grid-auto-flow:column;grid-auto-columns:210px;gap:18px;overflow-x:auto;padding-bottom:8px}
        .h-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:22px 18px}
        .h-card{display:flex;flex-direction:column;gap:5px;position:relative;background:#fff}
        .h-img{aspect-ratio:1;background:#f7f7f7;display:flex;align-items:center;justify-content:center;padding:16px;margin-bottom:4px;overflow:hidden}
        .h-img img{width:100%;height:100%;object-fit:contain;mix-blend-mode:multiply}
        .h-badge{position:absolute;top:0;left:0;z-index:1;background:#c45500;color:#fff;font-size:11px;font-weight:700;padding:4px 8px}
        .h-badge.deal{background:#cc0c39}
        .h-title{font-size:14px;line-height:1.4;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
        .h-title:hover{color:#c7511f}
        .h-rate{display:flex;align-items:center;gap:6px;font-size:13px}
        .stars{position:relative;display:inline-block;color:#d5d9d9;font-size:15px;letter-spacing:1px;line-height:1;white-space:nowrap}
        .stars span{position:absolute;left:0;top:0;overflow:hidden;color:#f5a00d}
        .h-price{font-size:22px;line-height:1;white-space:nowrap}.h-price sup{font-size:12px;position:relative;top:-.55em;vertical-align:baseline}
        .h-list{color:#565959;font-size:12px}
        .h-dealtag{background:#cc0c39;color:#fff;font-size:12px;font-weight:700;padding:3px 6px;border-radius:2px}
        .h-dealtxt{color:#cc0c39;font-size:12px;font-weight:700}
        .h-ship{font-size:12px;color:#565959}
        .h-bar{background:#fff;border-bottom:1px solid #ddd;box-shadow:0 1px 2px rgba(0,0,0,.06)}
        .h-bar .h-row{justify-content:space-between;font-size:14px;padding:10px 14px}
        .h-sort select{background:#f0f2f2;border:1px solid #d5d9d9;border-radius:8px;padding:4px 6px;font-size:13px}
        .h-results{display:grid;grid-template-columns:220px minmax(0,1fr);gap:24px;margin-top:16px}
        .h-results>div{background:#fff;padding:18px 20px}
        .h-filters h3{font-size:14px;margin:18px 0 6px}.h-filters h3:first-child{margin-top:0}
        .h-filters a,.h-filters b,.h-filters label{display:flex;align-items:center;gap:6px;font-size:14px;padding:3px 0}
        .h-filters a:hover{color:#c7511f;text-decoration:underline}
        .h-h1{font-size:24px}.h-sub{color:#565959;font-size:13px;margin:4px 0 16px}
        .h-crumbs{font-size:12px;color:#565959;padding:12px 0 8px}
        .h-pdp{background:#fff;padding:22px;display:grid;grid-template-columns:minmax(0,1.15fr) minmax(0,1fr) 260px;gap:30px}
        .h-gal{display:grid;grid-template-columns:54px minmax(0,1fr);gap:14px;align-self:start}
        .h-gal input{position:absolute;opacity:0;pointer-events:none}
        .h-thumbs{display:flex;flex-direction:column;gap:10px}
        .h-thumbs label{border:1px solid #bbb;border-radius:6px;padding:3px;cursor:pointer;background:#fff}
        .h-thumbs img{width:46px;height:46px;object-fit:contain}
        .h-main{aspect-ratio:1;display:flex;align-items:center;justify-content:center;background:#fff}
        .h-main img{display:none;max-height:100%;max-width:100%;object-fit:contain}
        #g0:checked~.h-main .g0,#g1:checked~.h-main .g1,#g2:checked~.h-main .g2,#g3:checked~.h-main .g3{display:block}
        #g0:checked~.h-thumbs [for=g0],#g1:checked~.h-thumbs [for=g1],#g2:checked~.h-thumbs [for=g2],#g3:checked~.h-thumbs [for=g3]{border-color:#e77600;box-shadow:0 0 3px 2px rgba(228,121,17,.5)}
        .h-info h1{font-size:24px;font-weight:400;line-height:1.3}
        .h-brand{font-size:14px;margin:4px 0}
        .h-info .h-rate{padding:4px 0 12px;border-bottom:1px solid #ddd}
        .h-info .h-rate a{color:#007185}
        .h-badge-i{background:#232f3e;color:#fff;font-size:12px;padding:3px 8px;border-radius:2px}
        .h-pp{margin:14px 0 2px;display:flex;align-items:flex-start;gap:8px}
        .h-pp .h-price{font-size:30px}
        .h-off{color:#cc0c39;font-size:26px;font-weight:300;line-height:1}
        .h-note{font-size:13px;color:#565959;margin:8px 0 14px;padding-bottom:14px;border-bottom:1px solid #ddd}
        .h-opt{margin:10px 0}
        .h-sw,.h-sizes{display:flex;flex-wrap:wrap;gap:8px;margin-top:8px}
        .h-sw span{width:62px;height:62px;border:1px solid #bbb;border-radius:6px;padding:4px;background:#fff}
        .h-sw span.on{border:2px solid #e77600}
        .h-sw img{width:100%;height:100%;object-fit:contain}
        .h-sizes span{border:1px solid #888;border-radius:8px;padding:6px 12px;font-size:13px;cursor:pointer}
        .h-sizes span:hover{background:#f7fafa}
        .h-about h2{font-size:16px;margin:18px 0 6px}
        .h-about ul{list-style:disc;padding-left:18px}.h-about li{margin:6px 0;line-height:1.45}
        .h-box{border:1px solid #d5d9d9;border-radius:8px;padding:18px;align-self:start}
        .h-box>.h-price{font-size:28px}
        .h-del{font-size:14px;margin:12px 0;line-height:1.45}
        .h-loc2{display:flex;align-items:center;gap:4px;font-size:12px;color:#007185}
        .h-instock{color:#007600;font-size:18px;margin:12px 0}
        .h-qty select{background:#f0f2f2;border:1px solid #d5d9d9;border-radius:8px;padding:4px 8px;margin-left:4px}
        .h-cta{display:block;width:100%;border-radius:20px;padding:9px;border:0;margin-top:10px;font-size:14px;cursor:pointer;color:#0f1111}
        .h-cta.add{background:#ffd814}.h-cta.add:hover{background:#f7ca00}.h-cta.buy{background:#ffa41c}.h-cta.buy:hover{background:#fa8900}
        .h-cta:disabled{opacity:.6;cursor:default}
        .h-box dl{display:grid;grid-template-columns:auto 1fr;gap:5px 12px;font-size:12px;margin-top:16px;color:#565959}
        .h-box dd{color:#0f1111}
        .h-spec{border-collapse:collapse;width:100%;max-width:720px;font-size:14px}
        .h-spec th{background:#f3f3f3;text-align:left;padding:9px 14px;width:42%;font-weight:700;border-bottom:1px solid #e7e7e7}
        .h-spec td{padding:9px 14px;border-bottom:1px solid #e7e7e7}
        .h-h2b{margin-top:24px}.h-desc{max-width:820px;line-height:1.55;font-size:14px}
        .h-revs{display:grid;grid-template-columns:300px minmax(0,1fr);gap:46px}
        .h-revs .h-rate{font-size:18px}.h-revs .stars{font-size:20px}
        .h-hist div{display:grid;grid-template-columns:50px minmax(0,1fr) 40px;gap:10px;align-items:center;margin:9px 0;color:#007185;font-size:14px}
        .h-meter{height:20px;background:#f0f2f2;border:1px solid #d5d9d9;border-radius:4px;overflow:hidden}.h-meter i{display:block;height:100%;background:#de7921}
        .h-revs h3{font-size:18px;margin-bottom:6px}
        .h-rev{padding:16px 0;border-bottom:1px solid #eee}
        .h-who{display:flex;gap:8px;align-items:center;font-size:13px;margin-bottom:6px}
        .h-av{width:30px;height:30px;border-radius:50%;background:#e3e6e6;color:#8d9096;display:flex;align-items:center;justify-content:center}
        .h-rt{display:flex;gap:8px;align-items:center;font-size:14px}
        .h-rd{font-size:12px;color:#565959;margin-top:4px}
        .h-vp{color:#c45500;font-weight:700}
        .h-rev p{margin:8px 0;line-height:1.5}
        .h-empty{display:grid;grid-template-columns:minmax(0,1fr) 280px;gap:30px;align-items:start;padding:30px}
        .h-empty h1{font-size:28px;font-weight:400;margin-bottom:10px}.h-empty p{color:#565959;margin-bottom:16px;max-width:520px}
        .h-sub-box{border:1px solid #d5d9d9;border-radius:8px;padding:18px;font-size:16px}
        .shopping .info{background:#fff;padding:30px;max-width:900px;margin-top:20px}
        .h-foot{background:#1f3a56;color:#dde;margin-top:30px}
        .h-top{display:block;background:#2e4b6b;text-align:center;padding:15px;font-size:13px}.h-top:hover{background:#38587b}
        .h-fcols{max-width:1000px;margin:0 auto;display:grid;grid-template-columns:repeat(4,1fr);gap:30px;padding:36px 20px}
        .h-fcols h4{color:#fff;font-size:16px;margin-bottom:10px}
        .h-fcols a{display:block;font-size:14px;padding:4px 0;color:#dde}.h-fcols a:hover{text-decoration:underline}
        .h-fbot{background:#102a43;padding:24px 14px 30px;text-align:center;font-size:12px}
        .h-fbot .h-logo{display:inline-flex;margin-bottom:10px}.h-fbot p a{margin-right:14px}.h-fbot p a:hover{text-decoration:underline}
        @media(max-width:1100px){.h-tiles{grid-template-columns:repeat(2,minmax(0,1fr))}.h-pdp{grid-template-columns:minmax(0,1fr) minmax(0,1fr)}.h-box{grid-column:span 2}}
        @media(max-width:700px){
          .h-head .h-row{flex-wrap:wrap;gap:8px 6px;padding:8px 10px}
          .h-logo{font-size:20px}.h-cart{margin-left:auto}
          .h-search{order:3;flex:1 0 100%}
          .hide-sm{display:none!important}
          .h-hero{grid-template-columns:1fr}.h-hero-text{padding:24px 20px}.h-hero h1{font-size:30px}.h-hero-img{min-height:200px}
          .h-tiles{grid-template-columns:1fr 1fr;gap:10px}.h-tile{padding:12px}.h-tile h2{font-size:16px}.h-quad{gap:8px}
          .h-page{padding:0 8px 20px}.h-sec{padding:14px 12px}
          .h-strip{grid-auto-columns:160px;gap:12px}
          .h-grid{grid-template-columns:1fr 1fr;gap:16px 10px}
          .h-results{grid-template-columns:1fr}.h-filters{display:none}.h-results>div{padding:14px 10px}
          .h-pdp{grid-template-columns:1fr;padding:14px}.h-box{grid-column:auto}
          .h-gal{grid-template-columns:1fr}.h-thumbs{flex-direction:row;order:2}
          .h-revs{grid-template-columns:1fr;gap:16px}
          .h-empty{grid-template-columns:1fr;padding:20px}
          .h-fcols{grid-template-columns:1fr 1fr;padding:24px 16px}
          .h-bar .h-row{flex-wrap:wrap;gap:6px}
        }
        """;

    #endregion

    private static T Pick<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Slug(string value) => World.Slug(value);

    private static string L(string name) => name.Split(' ')[^1];

    // one of a template's headline wordings, so a template used twice does not read the same
    private static string V(Facts f, params string[] options) => options[f.Variant % options.Length];

    // one of several quotes, chosen apart from the headline wording so recaps do not all sound alike
    private static string Q(Facts f, int salt, params string[] options) => options[(f.Number + salt) % options.Length];

    private static string Cap(string value) => string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Possessive(string name) => name.EndsWith('s') ? name + "'" : name + "'s";

    private static string The(Team team) => team.Club ? team.Full : "the " + team.Full;

    private static string Venue(Team team) => team.Full switch
    {
        _ when Nicknames["Basketball"].Any(n => team.Full.EndsWith(n)) || Nicknames["Hockey"].Any(n => team.Full.EndsWith(n)) => $"{team.City} Arena",
        _ when Nicknames["Baseball"].Any(n => team.Full.EndsWith(n)) => $"{team.City} Field",
        _ when team.Club => $"{team.City} Stadium",
        _ => $"{team.City} Memorial Stadium"
    };

    private static string Verb(Facts f)
    {
        var margin = f.Score - f.OtherScore;
        var (close, rout) = f.Sport switch { "Basketball" => (5, 20), "Football" or "College" => (4, 21), "Baseball" => (1, 7), _ => (1, 4) };
        return margin <= close ? new[] { "edge", "hold off", "slip past" }[f.Number % 3] : margin >= rout ? "rout" : new[] { "beat", "top", "get past" }[f.Number % 3];
    }

    private static string Injury(Facts f) => new[] { "ankle", "hamstring", "knee", "shoulder", "wrist" }[f.Number / 3 % 5];

    private static string Stat(Facts f) => f.Sport switch
    {
        "Football" or "College" => $"{f.Star} threw for {f.Number + 180} yards and {new[] { "one touchdown", "two touchdowns", "three touchdowns", "four touchdowns" }[Math.Clamp(f.Score / 7 - 1 - f.Number % 2, 0, 3)]}",
        "Basketball" => $"{f.Star} scored {f.Number % 18 + 22} points",
        "Baseball" => f.Score >= 3 ? $"{f.Star} drove in {Math.Min(f.Score - 1, f.Number % 3 + 2)} runs" : $"{f.Star} homered",
        "Soccer" => f.Score >= 2 ? $"{f.Star} scored twice" : $"{f.Star} scored the only goal",
        _ => $"{f.Star} made {f.Number % 8 + 20} saves"
    };

    private static string Turn(Facts f) => f.Sport switch
    {
        "Football" or "College" => $"The game turned early in the fourth quarter, when a strip sack near midfield set up a short touchdown run that gave {The(f.Team)} the lead for good.",
        "Basketball" => $"{Cap(The(f.Team))} broke it open with a 14-2 run late in the third quarter, forcing six turnovers in a span of four minutes.",
        "Baseball" => $"{Cap(The(f.Team))} broke a tie in the seventh inning on a two-out, two-run double into the left-field corner.",
        "Soccer" => "The decisive goal came in the 71st minute, a header at the near post from a corner kick.",
        _ => $"{Cap(The(f.Team))} took control with two power-play goals 1:42 apart in the second period."
    };

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in value)
                hash = hash * 31 + c;
            return hash & 0x7fffffff;
        }
    }

    private string Ago(DateTime published)
    {
        var minutes = (int)(world.ReferenceDate.AddHours(12) - published).TotalMinutes;
        return minutes < 60 ? $"{Math.Max(1, minutes)} min ago" : minutes < 60 * 24 ? $"{minutes / 60} hr ago" : published.ToString("MMM d", Us);
    }
}
