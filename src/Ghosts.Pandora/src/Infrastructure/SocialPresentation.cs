using System.Globalization;
using System.Text.RegularExpressions;
using Ghosts.Pandora.Infrastructure.Content;
using Ghosts.Pandora.Infrastructure.Models;

namespace Ghosts.Pandora.Infrastructure;

/// <summary>
/// What the social themes show around a post that the data does not hold: a display name and
/// headline for a username, a title and community for a post, a stock photo, plausible engagement
/// counts and relative times. Everything derives from the user name or post id, so a page looks the
/// same on every visit. Use it in a view with <c>@inject SocialPresentation Present</c>.
/// </summary>
public partial class SocialPresentation(World world)
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    // Stock photographs in wwwroot/img, used as post photos, video thumbnails and story images
    private static readonly string[] Photos =
    [
        .. new[] { 1, 2, 3, 4, 7, 8, 9, 10 }.Select(i => $"/img/last-photo{i}-large.jpg"),
        .. Enumerable.Range(1, 10).Select(i => $"/img/photo-album{i}.jpg"),
        .. Enumerable.Range(1, 11).Select(i => $"/img/photo-item{i}.jpg"),
        .. Enumerable.Range(1, 16).Select(i => $"/img/post{i}.jpg"),
        .. Enumerable.Range(1, 7).Select(i => $"/img/post-photo{i}.jpg"),
        .. Enumerable.Range(1, 17).Select(i => $"/img/video{i}.jpg")
    ];

    private static readonly (string Pattern, string Community)[] CommunityWords =
    [
        (@"\b(outage|server|network|help ?desk|tickets?|erp|it crew|it team|vpn|patch|backup)\b", "sysadmin"),
        (@"\b(coffee|espresso|restaurant|pizza|lunch|dinner|recipe|food)\b", "food"),
        (@"\b(5k|marathon|run|running|race)\b", "running"),
        (@"\b(traffic|route|bridge|road|downtown|mechanic|council|library)\b", "{city}"),
        (@"\b(proud|team|promotion|hired|hiring|job|career)\b", "jobs"),
        (@"\b(blood drive|food bank|volunteer|donat\w*|charity)\b", "{city}")
    ];

    private static readonly string[] FallbackCommunities = ["{city}", "AskReddit", "news", "pics", "CasualConversation", "LifeProTips"];

    private static readonly HashSet<string> NotTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        "The", "This", "That", "Anyone", "Our", "New", "Huge", "Great", "Reminder", "Looking", "Proud", "Thanks",
        "Took", "Friday", "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday", "Sunday", "I", "We", "It", "Just"
    };

    public World World => world;

    /// <summary>The person's full name when the username belongs to someone in the world, else the username made readable.</summary>
    public string DisplayName(string username)
    {
        var person = Person(username);
        if (person != null)
            return person.FullName;

        var words = Regex.Split(username ?? string.Empty, "[._-]+|(?<=[a-z])(?=[0-9])").Where(w => w.Length > 0 && !w.All(char.IsDigit)).ToList();
        return words.Count == 0 ? username : string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    public string Handle(string username) => $"@{username}";

    /// <summary>"Network Engineer at Westbrook Financial Group", or a plausible headline for someone outside the world.</summary>
    public string Headline(string username)
    {
        var person = Person(username);
        if (person != null)
            return $"{person.Title} at {world.OrganizationName}";

        string[] headlines =
        [
            "Project Manager | PMP", "Operations Specialist", "Customer Success Manager", "Marketing Coordinator",
            $"Small business owner in {world.City}", "Registered Nurse", "Logistics Coordinator", "Account Executive",
            "Graduate student", "Teacher and coach", "Freelance photographer", "Retired, volunteering locally"
        ];
        return headlines[Bucket(username, headlines.Length)];
    }

    /// <summary>
    /// The post's first line, or its first sentence when it is one line, as a title for platforms
    /// that need one. The NPC client posts a Reddit title as the first line of the message.
    /// </summary>
    public string Title(Post post) => Clip(SplitTitle(post).Title.TrimEnd('.'), 110);

    /// <summary>The rest of the post after <see cref="Title"/>, empty when the post is one sentence.</summary>
    public string Body(Post post) => SplitTitle(post).Body;

    private static (string Title, string Body) SplitTitle(Post post)
    {
        var text = Regex.Replace(post?.Message ?? string.Empty, "<[^>]+>", " ").Trim();
        var newline = text.IndexOf('\n');
        if (newline > 0)
            return (text[..newline].Trim(), PlainText(text[(newline + 1)..]));

        text = PlainText(text);
        var end = Regex.Match(text, @"[.!?](\s|$)");
        return end.Success && end.Index > 10 && end.Index + 1 < text.Length
            ? (text[..(end.Index + 1)], text[(end.Index + 1)..].Trim())
            : (text, string.Empty);
    }

    /// <summary>The message without the markup Pandora adds for uploads.</summary>
    public static string PlainText(Post post) => PlainText(post?.Message);

    private static string PlainText(string message) =>
        Regex.Replace(Regex.Replace(message ?? string.Empty, "<[^>]+>", " "), @"\s+", " ").Trim();

    /// <summary>The image a post shows: its uploaded image if it has one, else a stock photo chosen by its id.</summary>
    public string PhotoUrl(Post post, int variant = 0) => UploadedImage(post) ?? PhotoUrl(post.Id, variant);

    public string PhotoUrl(Guid id, int variant = 0) => Photos[Bucket($"{id}:{variant}", Photos.Length)];

    /// <summary>Whether a post carries a photo, for platforms where only some do. Uploaded images always count.</summary>
    public bool HasPhoto(Post post, int percent) => UploadedImage(post) != null || Bucket($"{post.Id}:photo", 100) < percent;

    public static string UploadedImage(Post post) =>
        Regex.Match(post?.Message ?? string.Empty, "<img src=\"(/images/[0-9a-f-]{36}/image\\.(?:jpg|png))\"") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>A Reddit-style community name without the "r/" prefix, from what the post is about.</summary>
    public string Community(Post post)
    {
        var text = PlainText(post);
        var community = CommunityWords.FirstOrDefault(c => Regex.IsMatch(text, c.Pattern, RegexOptions.IgnoreCase)).Community
                        ?? FallbackCommunities[Bucket(post.Id.ToString(), FallbackCommunities.Length)];
        return community.Replace("{city}", world.City.Replace(" ", ""));
    }

    /// <summary>
    /// Likes, reactions or votes: the likes the post really has, plus a stable baseline that grows in
    /// over its first six hours, so a post made a minute ago does not already show hundreds.
    /// </summary>
    public int Score(Post post)
    {
        var age = DateTime.UtcNow - DateTime.SpecifyKind(post.CreatedUtc, DateTimeKind.Utc);
        var ramp = Math.Clamp(age.TotalHours / 6, 0, 1);
        return (int)Math.Round(Baseline(post.Id, "score", 2, 900) * ramp) + (post.Likes?.Count ?? 0);
    }

    public int CommentCount(Post post) => post.Comments?.Count ?? 0;

    public int Shares(Post post) => Score(post) / (4 + Bucket($"{post.Id}:shares", 9));

    public int Views(Post post) => Score(post) * (12 + Bucket($"{post.Id}:views", 60)) + Bucket($"{post.Id}:v", 997);

    /// <summary>A video length such as "12:04".</summary>
    public string Duration(Guid id)
    {
        var seconds = 45 + Bucket($"{id}:duration", 1500);
        return seconds >= 3600 ? TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss") : TimeSpan.FromSeconds(seconds).ToString(@"m\:ss");
    }

    /// <summary>"now", "5m", "3h", "2d", then "Nov 3" (X, Instagram, Discord style).</summary>
    public static string ShortAgo(DateTime createdUtc)
    {
        var age = DateTime.UtcNow - DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc);
        if (age < TimeSpan.FromMinutes(1)) return "now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes}m";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours}h";
        if (age < TimeSpan.FromDays(7)) return $"{(int)age.TotalDays}d";
        return createdUtc.ToString(createdUtc.Year == DateTime.UtcNow.Year ? "MMM d" : "MMM d, yyyy", Us);
    }

    /// <summary>"just now", "5 minutes ago", "3 hours ago", "2 days ago", "3 weeks ago" (Facebook, YouTube, Reddit style).</summary>
    public static string LongAgo(DateTime createdUtc)
    {
        var age = DateTime.UtcNow - DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc);
        static string Unit(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")} ago";
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return Unit((int)age.TotalMinutes, "minute");
        if (age < TimeSpan.FromDays(1)) return Unit((int)age.TotalHours, "hour");
        if (age < TimeSpan.FromDays(7)) return Unit((int)age.TotalDays, "day");
        if (age < TimeSpan.FromDays(31)) return Unit((int)(age.TotalDays / 7), "week");
        if (age < TimeSpan.FromDays(365)) return Unit((int)(age.TotalDays / 30), "month");
        return Unit((int)(age.TotalDays / 365), "year");
    }

    /// <summary>1,234 as "1.2K", 3,400,000 as "3.4M".</summary>
    public static string Compact(int n) => n switch
    {
        >= 1_000_000 => (n / 1_000_000d).ToString(n >= 10_000_000 ? "0" : "0.#", Us) + "M",
        >= 1_000 => (n / 1_000d).ToString(n >= 10_000 ? "0" : "0.#", Us) + "K",
        _ => n.ToString(Us)
    };

    /// <summary>Topics people are posting about: hashtags and capitalized names in recent posts, then the world's own.</summary>
    public IReadOnlyList<(string Topic, int Posts)> Trending(IEnumerable<Post> posts, int count = 5)
    {
        var topics = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var post in posts ?? [])
        {
            var text = PlainText(post);
            foreach (Match match in TopicPattern().Matches(text))
            {
                var topic = match.Value.Trim();
                if (topic.Length < 3 || NotTopics.Contains(topic) || text.StartsWith(topic, StringComparison.Ordinal) && !topic.StartsWith('#'))
                    continue;
                topics[topic] = topics.GetValueOrDefault(topic) + 1;
            }
        }

        foreach (var topic in new[] { world.City, world.ShortName, world.Projects[0], "Weekend" })
            topics.TryAdd(topic, 0);

        return topics
            .OrderByDescending(t => t.Value)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(t => (t.Key, t.Value * 37 + Baseline(Guid.Empty, t.Key, 1200, 48000)))
            .ToList();
    }

    [GeneratedRegex(@"#\w+|\b[A-Z][a-z]+(?:\s[A-Z][a-z]+)*\b|\b[A-Z]{2,}\b")]
    private static partial Regex TopicPattern();

    private Person Person(string username) =>
        world.People.FirstOrDefault(p => string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

    private static int Bucket(string key, int buckets) => (int)(ContentRequest.StableHash(key ?? string.Empty) % (uint)buckets);

    // Skewed toward small numbers, the way most posts get little and a few get a lot
    private static int Baseline(Guid id, string salt, int min, int max)
    {
        var unit = Bucket($"{id}:{salt}", 10_000) / 10_000d;
        return min + (int)((max - min) * unit * unit * unit);
    }

    private static string Clip(string text, int length)
    {
        if (text.Length <= length)
            return text;
        var cut = text.LastIndexOf(' ', length);
        return text[..(cut > 0 ? cut : length)] + "…";
    }
}
