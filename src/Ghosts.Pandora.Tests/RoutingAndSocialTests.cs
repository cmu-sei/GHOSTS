using System.Net;
using System.Text.Json;

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Scenario B (social interactions persist), F (bad input fails safely) and G (extension routing
/// leaves the application's own routes alone).
/// </summary>
public class RoutingAndSocialTests : IClassFixture<RoutingAndSocialTests.Factory>
{
    public class Factory() : PandoraFactory(new Dictionary<string, string>
    {
        ["ApplicationConfiguration:Payloads:PayloadDirectory"] = PayloadDirectory,
        ["ApplicationConfiguration:Payloads:Mappings:0:Url"] = "/downloads/quarterly-report",
        ["ApplicationConfiguration:Payloads:Mappings:0:FileName"] = "fixture.pdf",
        ["ApplicationConfiguration:Payloads:Mappings:0:ContentType"] = "application/pdf"
    })
    {
        public static readonly string PayloadDirectory = Path.Combine(Path.GetTempPath(), $"pandora-payloads-{Guid.NewGuid():N}");
    }

    private static readonly byte[] PayloadBytes = "%PDF-1.4 training fixture"u8.ToArray();
    private readonly HttpClient _client;

    public RoutingAndSocialTests(Factory factory)
    {
        Directory.CreateDirectory(Factory.PayloadDirectory);
        File.WriteAllBytes(Path.Combine(Factory.PayloadDirectory, "fixture.pdf"), PayloadBytes);
        _client = factory.Client();
    }

    [Theory]
    [InlineData("/it/incident-report.pdf", "application/pdf")]
    [InlineData("/documents/report.pdf", "application/pdf")]
    [InlineData("/docs/memo.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("/files/data.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("/pdf/reports/quarterly-report", "application/pdf")]
    [InlineData("/data/users.json", "application/json")]
    [InlineData("/pandora/about", "application/json")]
    [InlineData("/api/posts", "application/json")]
    [InlineData("/login", "text/html")]
    [InlineData("/posts", "text/html")]
    [InlineData("/search?q=report.pdf", "text/html")]
    [InlineData("/", "text/html")]
    public async Task Routes_ReachTheRightHandler(string path, string mediaType)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Profile_OfAnUnknownUser_IsCreatedRatherThan404()
    {
        var response = await _client.GetAsync("/u/thalia.treutel?theme=youtube");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var user = JsonDocument.Parse(await _client.GetStringAsync("/api/users/thalia.treutel"));
        Assert.Equal("thalia.treutel", user.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Payload_IsServedAtItsConfiguredUrl()
    {
        var response = await _client.GetAsync("/downloads/quarterly-report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PayloadBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("/?theme=bogus")]
    [InlineData("/?theme=..%2F..%2FViews%2FShared")]
    [InlineData("/search?q=outage&theme=bogus")]
    public async Task UnknownTheme_FallsBackInsteadOfFailing(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/json/..%2F..%2Fappsettings.json")]
    [InlineData("/text/%2E%2E%2F%2E%2E%2Fsecret.txt")]
    public async Task EncodedTraversal_IsGeneratedNotRead(string path)
    {
        var response = await _client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("ConnectionStrings", body);
    }

    private async Task<Guid> CreatePost(string user, string message)
    {
        var response = await _client.PostAsync("/api/posts?theme=facebook",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["user"] = user, ["message"] = message }));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var posts = JsonDocument.Parse(await _client.GetStringAsync("/api/posts"));
        return posts.RootElement.EnumerateArray()
            .First(p => p.GetProperty("message").GetString() == message)
            .GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Post_Comment_And_Like_ArePersisted()
    {
        var message = $"Power is back on at the north office {Guid.NewGuid():N}";
        var id = await CreatePost("jsmith", message);

        var comment = await _client.PostAsync($"/posts/{id}/comments",
            new MultipartFormDataContent { { new StringContent("Good to hear, thanks for the update"), "message" } });
        Assert.Equal(HttpStatusCode.NoContent, comment.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/posts/{id}/likes", null)).StatusCode);

        using var detail = JsonDocument.Parse(await _client.GetStringAsync($"/api/posts/{id}/detail"));
        Assert.Equal(message, detail.RootElement.GetProperty("message").GetString());
        Assert.Equal("jsmith", detail.RootElement.GetProperty("username").GetString());
        Assert.Equal("Good to hear, thanks for the update",
            detail.RootElement.GetProperty("comments").EnumerateArray().Single().GetProperty("message").GetString());
        Assert.Single(detail.RootElement.GetProperty("likes").EnumerateArray());
    }

    [Fact]
    public async Task PostMarkup_IsShownAsText()
    {
        var marker = Guid.NewGuid().ToString("N");
        var id = await CreatePost("mallory", $"<script>alert('{marker}')</script><img src=x onerror=alert(1)>");

        var feed = await _client.GetStringAsync("/posts?theme=facebook");
        var detail = await _client.GetStringAsync($"/posts/{id}?theme=facebook");

        foreach (var page in new[] { feed, detail })
        {
            Assert.DoesNotContain($"<script>alert('{marker}')", page);
            Assert.DoesNotContain("<img src=x onerror", page);
        }
        Assert.Contains($"&lt;script&gt;alert(&#39;{marker}&#39;)", feed);
    }
}
