using DocumentFormat.OpenXml.Packaging;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Generated documents must round-trip: the package has to be flushed before the bytes are returned.
/// </summary>
public class OfficeDocumentGenerationServiceTests
{
    private readonly OfficeDocumentGenerationService _service = new(NullLogger<OfficeDocumentGenerationService>.Instance);

    [Fact]
    public void GenerateWordDocument_ContainsTitleAndContent()
    {
        var bytes = _service.GenerateWordDocument("My Title", "My content");

        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("My Title", text);
        Assert.Contains("My content", text);
    }

    [Fact]
    public void GeneratePowerPointDocument_ContainsSlides()
    {
        var bytes = _service.GeneratePowerPointDocument("Deck Title");

        using var doc = PresentationDocument.Open(new MemoryStream(bytes), false);

        Assert.NotEmpty(doc.PresentationPart!.SlideParts);
        Assert.Contains(doc.PresentationPart.SlideParts, s => s.Slide!.InnerText.Contains("Deck Title"));
    }
}
