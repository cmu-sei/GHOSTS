using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IOfficeDocumentGenerationService
{
    byte[] GenerateWordDocument(string title = null, string content = null);
    byte[] GenerateExcelDocument(string sheetName = null);
    byte[] GeneratePowerPointDocument(string title = null);
}

public class OfficeDocumentGenerationService : IOfficeDocumentGenerationService
{
    private readonly ILogger<OfficeDocumentGenerationService> _logger;
    private static readonly Random Random = new();

    public OfficeDocumentGenerationService(ILogger<OfficeDocumentGenerationService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateWordDocument(string title = null, string content = null)
    {
        title ??= ContentGenerationHelper.GenerateRandomTitle();
        _logger.LogInformation("Generating Word document with title: {Title}", title);

        using var memoryStream = new MemoryStream();
        using var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document);

        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new W.Document();
        var body = mainPart.Document.AppendChild(new W.Body());

        var titleParagraph = body.AppendChild(new W.Paragraph());
        var titleRun = titleParagraph.AppendChild(new W.Run());
        titleRun.AppendChild(new W.RunProperties(new W.Bold()));
        titleRun.AppendChild(new W.Text(title));

        if (content != null)
        {
            var contentParagraph = body.AppendChild(new W.Paragraph());
            var contentRun = contentParagraph.AppendChild(new W.Run());
            contentRun.AppendChild(new W.Text(content));
        }
        else
        {
            var paragraphCount = Random.Next(3, 10);
            var paragraphs = ContentGenerationHelper.GenerateRandomParagraphs(paragraphCount);

            foreach (var paragraph in paragraphs)
            {
                var para = body.AppendChild(new W.Paragraph());
                var run = para.AppendChild(new W.Run());
                run.AppendChild(new W.Text(paragraph));
            }
        }

        mainPart.Document.Save();

        return memoryStream.ToArray();
    }

    public byte[] GenerateExcelDocument(string sheetName = null)
    {
        sheetName ??= ContentGenerationHelper.GenerateRandomTitle();
        if (sheetName.Length > 31)
        {
            sheetName = sheetName.Substring(0, 31);
        }

        _logger.LogInformation("Generating Excel document with sheet: {SheetName}", sheetName);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        var rowCount = Random.Next(5, 20);
        var columnCount = 5;

        var headers = new[] { "Name", "Email", "Company", "Phone", "Address" };
        for (var col = 0; col < columnCount; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        for (var row = 2; row <= rowCount + 1; row++)
        {
            worksheet.Cell(row, 1).Value = Faker.Name.FullName();
            worksheet.Cell(row, 2).Value = Faker.Internet.Email();
            worksheet.Cell(row, 3).Value = Faker.Company.Name();
            worksheet.Cell(row, 4).Value = Faker.Phone.Number();
            worksheet.Cell(row, 5).Value = Faker.Address.StreetAddress();
        }

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    public byte[] GeneratePowerPointDocument(string title = null)
    {
        title ??= ContentGenerationHelper.GenerateRandomTitle();
        _logger.LogInformation("Generating PowerPoint document with title: {Title}", title);

        using var memoryStream = new MemoryStream();
        using var presentationDocument = PresentationDocument.Create(memoryStream, PresentationDocumentType.Presentation);

        var presentationPart = presentationDocument.AddPresentationPart();
        presentationPart.Presentation = new Presentation();

        CreatePresentationParts(presentationPart);

        var slideMasterIdList = new SlideMasterIdList(new SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" });
        var slideIdList = new SlideIdList();

        presentationPart.Presentation.Append(slideMasterIdList, slideIdList);

        var slideCount = Random.Next(3, 6);
        for (var i = 0; i < slideCount; i++)
        {
            var slideTitle = i == 0 ? title : ContentGenerationHelper.GenerateRandomTitle();
            var slideContent = Faker.Lorem.Paragraph();

            var slidePart = CreateSlidePart(presentationPart, slideTitle, slideContent);
            var slideId = new SlideId
            {
                Id = (uint)(256 + i),
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            };
            slideIdList.Append(slideId);
        }

        presentationPart.Presentation.Save();

        return memoryStream.ToArray();
    }

    private void CreatePresentationParts(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
        slideMasterPart.SlideMaster = new SlideMaster(
            new P.CommonSlideData(new P.ShapeTree()),
            new P.ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
            });

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
        slideLayoutPart.SlideLayout = new SlideLayout(
            new CommonSlideData(new ShapeTree()),
            new ColorMapOverride(new A.MasterColorMapping()));
    }

    private SlidePart CreateSlidePart(PresentationPart presentationPart, string title, string content)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        slidePart.Slide = new Slide(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new A.TransformGroup()),
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                            new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                            new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
                        new P.ShapeProperties(),
                        new P.TextBody(
                            new A.BodyProperties(),
                            new A.ListStyle(),
                            new A.Paragraph(new A.Run(new A.Text { Text = title })))))),
            new ColorMapOverride(new A.MasterColorMapping()));

        return slidePart;
    }
}
