using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Content;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghosts.Pandora.Tests;

public class ContentRequestTests
{
    private static readonly World World = World.Create(PandoraFactory.Seed, new DateTime(2026, 11, 19));

    [Theory]
    [InlineData("reports/2026/q3/network-assessment.docx", DocumentKind.NetworkAssessment, "Network Assessment", "Information Technology")]
    [InlineData("finance/quarterly/budget.xlsx", DocumentKind.Budget, "Budget", "Finance")]
    [InlineData("employees/directory.json", DocumentKind.Personnel, "Directory", "Human Resources")]
    [InlineData("hr/policies/password.pdf", DocumentKind.Policy, "Password", "Human Resources")]
    [InlineData("ops/minutes/2026-11-03-staff-meeting.docx", DocumentKind.MeetingMinutes, "Staff Meeting", "Operations")]
    [InlineData("index.docx", DocumentKind.Memo, "Memorandum", "Executive Office")]
    public void Parse_ReadsKindTitleAndDepartmentFromThePath(string path, DocumentKind kind, string title, string department)
    {
        var request = ContentRequest.Parse(path, World);

        Assert.Equal(kind, request.Kind);
        Assert.Equal(title, request.Title);
        Assert.Equal(department, request.Department);
    }

    [Fact]
    public void Parse_DatesAQuarterAtItsEnd()
    {
        var request = ContentRequest.Parse("reports/2026/q3/network-assessment.docx", World);

        Assert.Equal("Q3 2026", request.Period);
        Assert.Equal(new DateTime(2026, 9, 30), request.Date);
    }

    [Fact]
    public void Parse_GivesEveryFormatOfOnePathTheSameSeed()
    {
        Assert.Equal(ContentRequest.Parse("finance/budget.xlsx", World).Seed, ContentRequest.Parse("finance/budget.csv", World).Seed);
        Assert.NotEqual(ContentRequest.Parse("finance/budget.xlsx", World).Seed, ContentRequest.Parse("finance/forecast.xlsx", World).Seed);
    }

    [Fact]
    public void World_IsTheSameForTheSameSeed()
    {
        var again = World.Create(PandoraFactory.Seed, new DateTime(2026, 11, 19));

        Assert.Equal(World.OrganizationName, again.OrganizationName);
        Assert.Equal(World.People, again.People);
        Assert.Equal(World.People.Count, World.People.Select(p => p.Username).Distinct().Count());
        Assert.EndsWith(".example", World.Domain);
    }

    [Fact]
    public void Composer_IsDeterministic()
    {
        var composer = new DocumentComposer(World);
        var request = ContentRequest.Parse("it/outage-report.docx", World);

        var first = composer.Compose(request);
        var second = composer.Compose(request);

        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Sections.SelectMany(s => s.Paragraphs), second.Sections.SelectMany(s => s.Paragraphs));
    }

    [Theory]
    [InlineData("ops/minutes/staff-meeting.docx", "Action Items")]
    [InlineData("projects/erp-upgrade-status.docx", "Milestones")]
    [InlineData("finance/budget.docx", "Budget vs Actual")]
    [InlineData("it/outage-report.docx", "Timeline")]
    public void ReportTable_MatchesTheSpreadsheetForTheSamePath(string path, string heading)
    {
        var composer = new DocumentComposer(World);
        var request = ContentRequest.Parse(path, World);

        var inReport = composer.Compose(request).Sections.Single(s => s.Heading == heading).Table!;
        var table = composer.ComposeTable(request);

        Assert.Equal(table.Columns, inReport.Columns);
        Assert.Equal(table.Rows.Select(r => string.Join("|", r.Select(DataTable.Format))),
            inReport.Rows.Select(r => string.Join("|", r.Select(DataTable.Format))));
    }

    [Fact]
    public void AssessmentFindings_MatchTheInventoryCounts()
    {
        var composer = new DocumentComposer(World);
        var request = ContentRequest.Parse("it/network-assessment.docx", World);

        var findings = composer.Compose(request).Sections.Single(s => s.Heading == "Findings").Table!;
        var inventory = composer.ComposeTable(request);

        foreach (var row in inventory.Rows)
        {
            var host = ((string)row[0]).Split('.')[0];
            var open = findings.Rows.Count(f => (string)f[3] == host && (string)f[4] != "Accepted risk");
            Assert.Equal(open, (int)row[8]);
        }
    }
}

public class ContentManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pandora-store-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private ContentManager Store(bool storeResults = true, int maxMegabytes = 1024) => new(
        new ApplicationConfiguration { Pandora = { ContentCacheDirectory = _root, StoreResults = storeResults, MaxStoredMegabytes = maxMegabytes } },
        new Environment(),
        NullLogger<ContentManager>.Instance);

    private class Environment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Ghosts.Pandora";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("a/../../secret.txt")]
    [InlineData("a/./b.txt")]
    [InlineData("a\\..\\b.txt")]
    [InlineData("c:/windows/win.ini")]
    public void SafeRelativePath_RejectsAnythingButPlainSegments(string path)
    {
        Assert.Null(ContentManager.SafeRelativePath("txt", path, "txt"));
    }

    [Fact]
    public void SafeRelativePath_KeepsTheServedExtension()
    {
        Assert.Equal(Path.Combine("docx", "memos", "update.docx"), ContentManager.SafeRelativePath("docx", "memos/update.doc", "docx"));
        Assert.Equal(Path.Combine("pdf", "index.pdf"), ContentManager.SafeRelativePath("pdf", "", "pdf"));
    }

    [Fact]
    public void GetOrCreate_GeneratesOnce_EvenUnderConcurrency()
    {
        var store = Store();
        var calls = 0;

        var results = Enumerable.Range(0, 16).AsParallel()
            .Select(_ => store.GetOrCreate("txt", "notes/today.txt", "txt", () =>
            {
                Interlocked.Increment(ref calls);
                Thread.Sleep(20);
                return [1, 2, 3];
            }))
            .ToList();

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal(new byte[] { 1, 2, 3 }, r));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void GetOrCreate_DoesNotStoreWhenDisabledOrFull()
    {
        var calls = 0;
        byte[] Generate() { calls++; return [9]; }

        Store(storeResults: false).GetOrCreate("txt", "a.txt", "txt", Generate);
        Store(storeResults: false).GetOrCreate("txt", "a.txt", "txt", Generate);
        Store(maxMegabytes: 0).GetOrCreate("txt", "b.txt", "txt", Generate);

        Assert.Equal(3, calls);
        Assert.False(Directory.Exists(Path.Combine(_root, "txt")));
    }
}

public class MessageHtmlTests
{
    [Fact]
    public void EncodesMarkup()
    {
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", MessageHtml.ToHtml("<script>alert(1)</script>"));
    }

    [Fact]
    public void KeepsPandorasOwnUploadedImage()
    {
        const string image = "/images/0f8fad5b-d9cb-469f-a165-70867728950e/image.jpg";

        Assert.Equal($"Lunch &amp; learn<br/><img src=\"{image}\"/>", MessageHtml.ToHtml($"Lunch & learn<br/><img src=\"{image}\"/>"));
    }

    [Theory]
    [InlineData("<img src=\"/images/0f8fad5b-d9cb-469f-a165-70867728950e/image.jpg\" onerror=\"alert(1)\"/>")]
    [InlineData("<img src=\"https://example.com/image.jpg\"/>")]
    [InlineData("<img src=\"/images/../x/image.jpg\"/>")]
    public void EncodesImagesItDidNotWrite(string message)
    {
        Assert.DoesNotContain("<img", MessageHtml.ToHtml(message));
    }
}

public class ThemeCookieTests
{
    [Theory]
    [InlineData("localhost:6366", "theme-6366")]
    [InlineData("localhost:6360", "theme-6360")]
    [InlineData("facebook.range.example", "theme")]
    public void EachSiteOnASharedHostKeepsItsOwnThemeCookie(string host, string cookie)
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Request.Host = new Microsoft.AspNetCore.Http.HostString(host);

        Assert.Equal(cookie, ThemeViewLocationExpander.CookieName(http));
    }
}
