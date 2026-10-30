// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class SourceTests
{
    private static Task<HttpResponseMessage> AddText(HttpClient client, int id, string name, string text) =>
        client.PostAsJsonAsync($"/api/scenarios/{id}/sources/text", new { name, text });

    private static Task<HttpResponseMessage> Upload(HttpClient client, int id, string fileName, byte[] bytes) =>
        client.PostAsync($"/api/scenarios/{id}/sources/file", new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } });

    private static async Task<JsonArray> Sources(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/sources"))!;

    private static async Task<JsonArray> Search(HttpClient client, int id, string q) =>
        (await client.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/sources/search?q={Uri.EscapeDataString(q)}"))!;

    private static byte[] Pdf(string? text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        if (text is not null)
            page.AddText(text, 12, new PdfPoint(40, 700), font);
        return builder.Build();
    }

    private static byte[] Docx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            doc.AddMainDocumentPart().Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p)))).ToArray<OpenXmlElement>()));
        return stream.ToArray();
    }

    [Fact]
    public async Task A_text_source_is_listed_without_its_text()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var added = await AddText(owner, id, "Port notice", "The cranes stopped at 14:10.");

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var source = (await Sources(owner, id)).Single()!;
        Assert.Equal("Port notice", (string)source["name"]!);
        Assert.Equal("text", (string)source["kind"]!);
        Assert.Equal(28, (int)source["characters"]!);
        Assert.Equal("dev", (string)source["addedBy"]!);
        Assert.Null(source["text"]);
        Assert.Equal("NAME_REQUIRED", await Code(await AddText(owner, id, " ", "Some text.")));
        Assert.Equal("TEXT_REQUIRED", await Code(await AddText(owner, id, "Empty", " ")));
    }

    [Fact]
    public async Task Pdf_docx_and_html_uploads_are_read_as_text()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        Assert.Equal(HttpStatusCode.Created, (await Upload(owner, id, "notice.pdf", Pdf("Harbor cranes stop at 14:10."))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Upload(owner, id, "findings.docx", Docx("The cranes lost power.", "The transfer switch was in manual."))).StatusCode);
        var html = "<html><head><title>x</title><script>stealCranes()</script></head><body><p>Crane operators &amp; the port.</p></body></html>";
        Assert.Equal(HttpStatusCode.Created, (await Upload(owner, id, "page.html", Encoding.UTF8.GetBytes(html))).StatusCode);

        var names = (await Sources(owner, id)).Select(s => (string)s!["name"]!).ToList();
        Assert.Equal(["notice.pdf", "findings.docx", "page.html"], names);
        var passages = await Search(owner, id, "cranes");
        Assert.Contains(passages, p => ((string)p!["text"]!).Contains("Harbor cranes stop at 14:10."));
        Assert.Contains(passages, p => ((string)p!["text"]!).Contains("The cranes lost power.\nThe transfer switch was in manual."));
        var page = (string)passages.Single(p => (string)p!["source"]!["name"]! == "page.html")!["text"]!;
        Assert.Contains("Crane operators & the port.", page);
        Assert.DoesNotContain("stealCranes", page);
    }

    [Fact]
    public async Task Unsupported_empty_or_oversized_sources_are_refused_with_the_reason()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var sheet = await Upload(owner, id, "budget.xlsx", [1, 2, 3]);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, sheet.StatusCode);
        var body = await Body(sheet);
        Assert.Equal("SOURCE_UNSUPPORTED", (string)body["code"]!);
        Assert.Contains(".pdf", (string)body["message"]!);

        var broken = await Upload(owner, id, "broken.pdf", [1, 2, 3]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
        Assert.Equal("SOURCE_UNREADABLE", await Code(broken));

        var scanned = await Upload(owner, id, "scan.pdf", Pdf(null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, scanned.StatusCode);
        Assert.Equal("SOURCE_EMPTY", await Code(scanned));
        Assert.Equal("SOURCE_EMPTY", await Code(await Upload(owner, id, "blank.txt", Encoding.UTF8.GetBytes("  \n "))));

        var huge = await AddText(owner, id, "Huge", new string('x', 1_000_001));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, huge.StatusCode);
        Assert.Equal("SOURCE_TOO_LARGE", await Code(huge));
        Assert.Empty(await Sources(owner, id));
    }

    [Fact]
    public async Task A_url_is_fetched_stripped_and_named_by_its_title()
    {
        using var factory = new ApiFactory
        {
            Web = request => request.RequestUri!.Host == "port.example"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html><head><title>Port notice</title><style>p{}</style></head><body><p>Berth 4 is closed.</p></body></html>", Encoding.UTF8, "text/html")
                }
                : throw new HttpRequestException("Name or service not known")
        };
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var added = await owner.PostAsJsonAsync($"/api/scenarios/{id}/sources/url", new { url = "https://port.example/notice" });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var source = (await Sources(owner, id)).Single()!;
        Assert.Equal("Port notice", (string)source["name"]!);
        Assert.Equal("url", (string)source["kind"]!);
        Assert.Equal("https://port.example/notice", (string)source["origin"]!);
        var text = (string)(await Search(owner, id, "berth")).Single()!["text"]!;
        Assert.Contains("Berth 4 is closed.", text);
        Assert.DoesNotContain("p{}", text);

        var unreachable = await owner.PostAsJsonAsync($"/api/scenarios/{id}/sources/url", new { url = "https://closed.range/x" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unreachable.StatusCode);
        var body = await Body(unreachable);
        Assert.Equal("URL_UNREACHABLE", (string)body["code"]!);
        Assert.Contains("paste", (string)body["message"]!);
        Assert.Equal("URL_INVALID", await Code(await owner.PostAsJsonAsync($"/api/scenarios/{id}/sources/url", new { url = "file:///etc/passwd" })));
    }

    [Fact]
    public async Task Search_ranks_passages_by_the_query_words()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AddText(owner, id, "Weather", "A storm surge reached the harbor wall.");
        await AddText(owner, id, "Maintenance log", "The generator room flooded. The generator failed; the backup generator never started.");

        var passages = await Search(owner, id, "generator room");

        Assert.Equal("Maintenance log", (string)passages[0]!["source"]!["name"]!);
        Assert.Equal(1, (int)passages[0]!["passage"]!);
        Assert.Equal(1, (int)passages[0]!["of"]!);
        Assert.DoesNotContain(passages, p => (string)p!["source"]!["name"]! == "Weather");
        Assert.Empty(await Search(owner, id, "ransomware"));
    }

    [Fact]
    public async Task A_long_source_is_searched_in_passages()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        var paragraphs = Enumerable.Range(1, 40).Select(n => n == 33 ? "Paragraph 33 names the transfer switch." : $"Paragraph {n} is about the weather and nothing else at all, at some length.").ToList();
        await AddText(owner, id, "Long report", string.Join("\n\n", paragraphs));

        var passage = (await Search(owner, id, "transfer switch")).Single()!;

        Assert.Contains("Paragraph 33 names the transfer switch.", (string)passage["text"]!);
        Assert.True((int)passage["of"]! > 1);
        Assert.True(((string)passage["text"]!).Length <= 1_600);
    }

    [Fact]
    public async Task The_agent_searches_sources_but_cannot_add_or_remove_them()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var agent = factory.Client(agent: true);
        var id = await CreateScenario(owner);
        var sourceId = (int)(await Body(await AddText(owner, id, "Port notice", "The cranes stopped.")))["id"]!;

        var add = await AddText(agent, id, "Mine", "Made up.");
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal("AGENT_CANNOT_CHANGE_SOURCES", await Code(add));
        Assert.Equal("AGENT_CANNOT_CHANGE_SOURCES", await Code(await agent.DeleteAsync($"/api/scenarios/{id}/sources/{sourceId}")));
        Assert.Single(await Search(agent, id, "cranes"));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/scenarios/{id}/sources/{sourceId}")).StatusCode);
        Assert.Empty(await Sources(owner, id));
        Assert.Empty(await Search(owner, id, "cranes"));
        Assert.Equal("SOURCE_UNKNOWN", await Code(await owner.DeleteAsync($"/api/scenarios/{id}/sources/{sourceId}")));
    }

    [Fact]
    public async Task Progress_lists_the_sources()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AddText(owner, id, "Port notice", "The cranes stopped.");

        var source = (await Progress(owner, id))["sources"]!.AsArray().Single()!;

        Assert.Equal("Port notice", (string)source["name"]!);
        Assert.Equal(19, (int)source["characters"]!);
    }
}
