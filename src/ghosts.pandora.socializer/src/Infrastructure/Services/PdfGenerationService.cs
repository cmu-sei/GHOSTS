using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IPdfGenerationService
{
    byte[] GeneratePdf(string title = null, string content = null);
}

public class PdfGenerationService : IPdfGenerationService
{
    private readonly ILogger<PdfGenerationService> _logger;
    private static readonly Random Random = new();

    public PdfGenerationService(ILogger<PdfGenerationService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(string title = null, string content = null)
    {
        title ??= ContentGenerationHelper.GenerateRandomTitle();
        _logger.LogInformation("Generating PDF with title: {Title}", title);

        var paragraphCount = Random.Next(5, 15);
        var paragraphs = content != null
            ? new List<string> { content }
            : ContentGenerationHelper.GenerateRandomParagraphs(paragraphCount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text(title)
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(20);

                        foreach (var paragraph in paragraphs)
                        {
                            x.Item().Text(paragraph);
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }
}
