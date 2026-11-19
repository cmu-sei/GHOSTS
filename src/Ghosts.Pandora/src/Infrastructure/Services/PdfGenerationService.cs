using Ghosts.Pandora.Infrastructure.Content;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IPdfGenerationService
{
    byte[] GeneratePdf(DocumentSpec spec);
}

public class PdfGenerationService : IPdfGenerationService
{
    private const string Navy = "#1F3864";
    private const string Blue = "#2F5496";
    private const string HeaderFill = "#D9E2F3";

    private readonly ILogger<PdfGenerationService> _logger;

    public PdfGenerationService(ILogger<PdfGenerationService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(DocumentSpec spec)
    {
        _logger.LogInformation("Generating PDF: {Title}", spec.Title);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10.5f));

                page.Header().Column(header =>
                {
                    header.Item().Text(spec.Organization.ToUpperInvariant()).FontSize(8).FontColor(Colors.Grey.Darken1);
                    header.Item().Text(spec.Title).FontSize(20).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(spec.Subtitle))
                        header.Item().Text(spec.Subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
                    header.Item().PaddingTop(4)
                        .Text($"Prepared by {spec.Author.FullName}, {spec.Author.Title} | {spec.Date:MMMM d, yyyy} | {spec.Classification}")
                        .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(Navy);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(6);
                    foreach (var section in spec.Sections)
                    {
                        content.Item().PaddingTop(8).Text(section.Heading).FontSize(13).Bold().FontColor(Blue);
                        foreach (var paragraph in section.Paragraphs)
                            content.Item().Text(paragraph).LineHeight(1.3f);
                        foreach (var bullet in section.Bullets)
                        {
                            content.Item().PaddingLeft(8).Row(row =>
                            {
                                row.ConstantItem(12).Text("•");
                                row.RelativeItem().Text(bullet).LineHeight(1.3f);
                            });
                        }
                        if (section.Table != null)
                            content.Item().PaddingTop(4).Element(e => Table(e, section.Table));
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{spec.Organization} | {spec.Classification}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        });

        return document
            .WithMetadata(new DocumentMetadata
            {
                Title = spec.Title,
                Author = spec.Author.FullName,
                Subject = spec.Subtitle,
                Creator = "Microsoft Word",
                CreationDate = spec.Date,
                ModifiedDate = spec.Date
            })
            .GeneratePdf();
    }

    private static void Table(IContainer container, Content.DataTable table)
    {
        container.Table(t =>
        {
            // wider columns for longer content, so emails and descriptions wrap less
            t.ColumnsDefinition(columns =>
            {
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    var column = c;
                    var longest = table.Rows.Select(r => Content.DataTable.Format(r[column]).Length).DefaultIfEmpty(0).Max();
                    columns.RelativeColumn(Math.Clamp(Math.Max(longest, table.Columns[column].Length), 4, 40));
                }
            });

            t.Header(header =>
            {
                foreach (var column in table.Columns)
                    header.Cell().Background(HeaderFill).Padding(4).Text(column).Bold().FontSize(8.5f);
            });

            foreach (var row in table.Rows)
            {
                foreach (var value in row)
                    t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Content.DataTable.Format(value)).FontSize(8.5f);
            }
        });
    }
}
