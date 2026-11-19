using System.Text.Json;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Ghosts.Pandora.Infrastructure.Content;
using A = DocumentFormat.OpenXml.Drawing;

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Scenario A (dynamic document retrieval) and E (the same facts across formats), through the
/// real HTTP pipeline: file extension routing, content store and generators.
/// </summary>
public class GeneratedDocumentTests : IClassFixture<PandoraFactory>
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PptxType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private readonly PandoraFactory _factory;
    private readonly HttpClient _client;
    private readonly World _world;

    public GeneratedDocumentTests(PandoraFactory factory)
    {
        _factory = factory;
        _client = factory.Client();
        _world = World.Create(PandoraFactory.Seed, new DateTime(2026, 11, 19));
    }

    private async Task<(byte[] Body, HttpResponseMessage Response)> Get(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadAsByteArrayAsync(), response);
    }

    private static void AssertValid(OpenXmlPackage package)
    {
        var errors = new OpenXmlValidator().Validate(package).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public async Task Docx_IsValid_Contextual_AndStableAcrossRequests()
    {
        const string path = "/reports/2026/q3/network-assessment.docx";
        var (body, response) = await Get(path);

        Assert.Equal(DocxType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("network-assessment.docx", response.Content.Headers.ContentDisposition?.FileNameStar);

        using (var stream = new MemoryStream(body))
        using (var document = WordprocessingDocument.Open(stream, false))
        {
            AssertValid(document);
            var text = document.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("Network Assessment", text);
            Assert.Contains("Q3 2026", text);
            Assert.Contains(_world.OrganizationName, text);
            Assert.Contains("Findings", text);
            Assert.Contains(_world.Systems[0].Hostname, text);
            Assert.Equal(_world.OrganizationName, document.ExtendedFilePropertiesPart?.Properties?.Company?.Text);
            Assert.Contains(document.PackageProperties.Creator, _world.People.Select(p => p.FullName));
        }

        var (again, _) = await Get(path);
        Assert.Equal(body, again);
        Assert.True(File.Exists(Path.Combine(_factory.ContentDirectory, "docx", "reports", "2026", "q3", "network-assessment.docx")));
    }

    [Theory]
    [InlineData("/memos/staff-update.doc", "staff-update.docx")]
    [InlineData("/doc/memos/staff-update", "staff-update.docx")]
    public async Task LegacyAndBareWordRequests_GetARealDocxNamedAsOne(string path, string fileName)
    {
        var (body, response) = await Get(path);

        Assert.Equal(DocxType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fileName, response.Content.Headers.ContentDisposition?.FileNameStar);
        using var stream = new MemoryStream(body);
        using var document = WordprocessingDocument.Open(stream, false);
        AssertValid(document);
    }

    [Theory]
    [InlineData("/briefings/incident-review.pptx", "incident-review.pptx")]
    [InlineData("/deck.ppt", "deck.pptx")]
    public async Task Pptx_IsValid_WithATitleSlideAndContentSlides(string path, string fileName)
    {
        var (body, response) = await Get(path);

        Assert.Equal(PptxType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fileName, response.Content.Headers.ContentDisposition?.FileNameStar);
        using var stream = new MemoryStream(body);
        using var presentation = PresentationDocument.Open(stream, false);
        AssertValid(presentation);

        var slides = presentation.PresentationPart!.SlideParts.ToList();
        Assert.True(slides.Count >= 2, $"{slides.Count} slides");
        var text = string.Join(" ", slides.SelectMany(s => s.Slide!.Descendants<A.Text>()).Select(t => t.Text));
        Assert.Contains(_world.OrganizationName, text);
    }

    [Fact]
    public async Task BudgetWorkbook_HasTypedValuesFormulasAndTotals_MatchingTheCsv()
    {
        var (xlsx, response) = await Get("/finance/quarterly/budget.xlsx");
        Assert.Equal(XlsxType, response.Content.Headers.ContentType?.MediaType);

        using (var stream = new MemoryStream(xlsx))
        using (var document = SpreadsheetDocument.Open(stream, false))
            AssertValid(document);

        using var workbookStream = new MemoryStream(xlsx);
        using var workbook = new XLWorkbook(workbookStream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal(["Category", "Owner", "Budget", "Actual", "Variance"], sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.True(sheet.Cell(2, 3).Value.IsNumber);
        Assert.Equal("C2-D2", sheet.Cell(2, 5).FormulaA1);

        var totalRow = sheet.LastRowUsed()!.RowNumber();
        Assert.Equal("Total", sheet.Cell(totalRow, 1).GetString());
        Assert.StartsWith("SUM(C2:C", sheet.Cell(totalRow, 3).FormulaA1);

        // the CSV for the same path stem carries the same figures
        var (csv, _) = await Get("/finance/quarterly/budget.csv");
        var lines = System.Text.Encoding.UTF8.GetString(csv).Trim().Split('\n');
        Assert.Equal(totalRow - 2, lines.Length - 1);
        var firstBudget = decimal.Parse(lines[1].Split(',')[2], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(firstBudget, (decimal)sheet.Cell(2, 3).Value.GetNumber());
    }

    [Fact]
    public async Task Directory_ListsTheSamePeopleWhoAuthorDocuments()
    {
        var (json, response) = await Get("/employees/directory.json");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(_world.OrganizationName, parsed.RootElement.GetProperty("organization").GetString());
        var names = parsed.RootElement.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Equal(_world.People.Select(p => p.FullName), names);

        var (docx, _) = await Get("/it/incident-report.docx");
        using var document = WordprocessingDocument.Open(new MemoryStream(docx), false);
        Assert.Contains(document.PackageProperties.Creator, names);
    }

    [Fact]
    public async Task Pdf_IsAPdfWithTheRequestedTitle()
    {
        var (body, response) = await Get("/hr/policies/password.pdf");

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(body, 0, 4));
        Assert.Equal("password.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task Text_IsTheSameDocumentAsPlainText()
    {
        var (body, response) = await Get("/hr/policies/remote-work.txt");

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var text = System.Text.Encoding.UTF8.GetString(body);
        Assert.Contains("Remote Work Policy", text);
        Assert.Contains("1. PURPOSE", text);
    }
}
