using System.Net;
using System.Text.RegularExpressions;

namespace Ghosts.Pandora.Tests;

/// <summary>Scenario D: every link on the generated website leads to a page, and pages do not change.</summary>
public class WebsiteModeTests
{
    private static PandoraFactory Site(string siteType) => PandoraFactory.With(new Dictionary<string, string>
    {
        ["ApplicationConfiguration:Mode:Type"] = "website",
        ["ApplicationConfiguration:Mode:SiteType"] = siteType,
        ["ApplicationConfiguration:Mode:SiteName"] = "Riverton Ledger",
        ["ApplicationConfiguration:Mode:ArticleCount"] = "8"
    });

    [Theory]
    [InlineData("news")]
    [InlineData("sports")]
    [InlineData("entertainment")]
    [InlineData("shopping")]
    public async Task EveryLinkResolves(string siteType)
    {
        using var factory = Site(siteType);
        var client = factory.Client();

        var seen = new HashSet<string> { "/" };
        var queue = new Queue<string>(["/"]);
        var broken = new List<string>();
        while (queue.Count > 0 && seen.Count < 300)
        {
            var path = queue.Dequeue();
            var response = await client.GetAsync(path);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"{path} -> {(int)response.StatusCode}");
                continue;
            }

            // follow page links only; images go through the image generator, which needs native Skia
            foreach (Match link in Regex.Matches(await response.Content.ReadAsStringAsync(), "href=\"(/[^\"]*)\""))
            {
                if (seen.Add(link.Groups[1].Value))
                    queue.Enqueue(link.Groups[1].Value);
            }
        }

        Assert.Empty(broken);
        Assert.True(seen.Count > 20, $"only {seen.Count} pages were linked");
        Assert.Contains(seen, p => p.StartsWith(siteType == "shopping" ? "/product/" : "/article/"));
    }

    [Fact]
    public async Task Pages_AreStable_AndUnknownPagesStillRender()
    {
        using var factory = Site("news");
        var client = factory.Client();

        var first = await client.GetStringAsync("/");
        Assert.Equal(first, await client.GetStringAsync("/"));
        Assert.Contains("Riverton Ledger", first);

        var article = Regex.Match(first, "href=\"(/article/\\d+)\"").Groups[1].Value;
        Assert.Equal(await client.GetStringAsync(article), await client.GetStringAsync(article));

        // Pandora almost never answers 404: a made-up path gets a page of its own, the same one every time
        foreach (var path in new[] { "/article/1", "/careers", "/news/2026/11/04/storm-cleanup-continues-across-town" })
        {
            var page = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal(await page.Content.ReadAsStringAsync(), await client.GetStringAsync(path));
        }

        // only a path no site could have still gets the site's not-found page
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/a/b/c/d/e/f/g/h/i")).StatusCode);
    }
}
