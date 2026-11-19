using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Ghosts.Pandora.Infrastructure.Content;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using X = DocumentFormat.OpenXml.ExtendedProperties;
using DataTable = Ghosts.Pandora.Infrastructure.Content.DataTable;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IOfficeDocumentGenerationService
{
    byte[] GenerateWordDocument(DocumentSpec spec);
    byte[] GenerateExcelDocument(DocumentSpec about, DataTable table);
    byte[] GeneratePowerPointDocument(DocumentSpec spec);
}

public class OfficeDocumentGenerationService(ILogger<OfficeDocumentGenerationService> logger) : IOfficeDocumentGenerationService
{
    private const string Navy = "1F3864";
    private const string Blue = "2F5496";
    private const string HeaderFill = "D9E2F3";

    #region Word

    public byte[] GenerateWordDocument(DocumentSpec spec)
    {
        logger.LogInformation("Generating Word document: {Title}", spec.Title);

        using var stream = new MemoryStream();
        // the package is only written to the stream when it is disposed, so read the stream after this block
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            SetProperties(document, spec, "Microsoft Office Word");

            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = WordStyles();
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = BulletNumbering();

            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new W.Footer(new W.Paragraph(
                new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Meta" }),
                new W.Run(new W.Text($"{spec.Organization} | {spec.Classification} | Page ") { Space = SpaceProcessingModeValues.Preserve }),
                new W.SimpleField(new W.Run(new W.Text("1"))) { Instruction = " PAGE " }));

            var body = new W.Body();
            body.Append(WordParagraph(spec.Organization.ToUpperInvariant(), "Meta"));
            body.Append(WordParagraph(spec.Title, "Title"));
            if (!string.IsNullOrWhiteSpace(spec.Subtitle))
                body.Append(WordParagraph(spec.Subtitle, "Subtitle"));
            body.Append(WordParagraph($"Prepared by {spec.Author.FullName}, {spec.Author.Title} | {spec.Date:MMMM d, yyyy} | {spec.Classification}", "Meta"));

            foreach (var section in spec.Sections)
            {
                body.Append(WordParagraph(section.Heading, "Heading1"));
                foreach (var paragraph in section.Paragraphs)
                    body.Append(WordParagraph(paragraph, null));
                foreach (var bullet in section.Bullets)
                    body.Append(WordBullet(bullet));
                if (section.Table != null)
                {
                    body.Append(WordTable(section.Table));
                    body.Append(new W.Paragraph());
                }
            }

            body.Append(new W.SectionProperties(
                new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                new W.PageSize { Width = 12240U, Height = 15840U },
                new W.PageMargin { Top = 1440, Right = 1440U, Bottom = 1440, Left = 1440U, Header = 720U, Footer = 720U, Gutter = 0U }));

            main.Document = new W.Document(body);
        }

        return stream.ToArray();
    }

    private static W.Paragraph WordParagraph(string text, string style)
    {
        var paragraph = new W.Paragraph();
        if (style != null)
            paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));

        var lines = text.Split('\n');
        var run = new W.Run();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.Append(new W.Break());
            run.Append(new W.Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        paragraph.Append(run);
        return paragraph;
    }

    private static W.Paragraph WordBullet(string text) => new(
        new W.ParagraphProperties(
            new W.ParagraphStyleId { Val = "ListParagraph" },
            new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 1 })),
        new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static W.Table WordTable(DataTable table)
    {
        var result = new W.Table(new W.TableProperties(
            new W.TableStyle { Val = "TableGrid" },
            new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
            new W.TableLook { Val = "04A0" }));

        var grid = new W.TableGrid();
        foreach (var _ in table.Columns)
            grid.Append(new W.GridColumn { Width = (9360 / table.Columns.Count).ToString() });
        result.Append(grid);

        var header = new W.TableRow(new W.TableRowProperties(new W.TableHeader()));
        foreach (var column in table.Columns)
            header.Append(WordCell(column, true));
        result.Append(header);

        foreach (var row in table.Rows)
        {
            var tableRow = new W.TableRow();
            foreach (var value in row)
                tableRow.Append(WordCell(DataTable.Format(value), false));
            result.Append(tableRow);
        }
        return result;
    }

    private static W.TableCell WordCell(string text, bool header)
    {
        var cellProperties = new W.TableCellProperties();
        if (header)
            cellProperties.Append(new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = HeaderFill });

        var run = new W.Run();
        var runProperties = new W.RunProperties();
        if (header)
            runProperties.Append(new W.Bold());
        runProperties.Append(new W.FontSize { Val = "18" });
        run.Append(runProperties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });

        return new W.TableCell(cellProperties,
            new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { Before = "40", After = "40" }), run));
    }

    private static W.Styles WordStyles()
    {
        W.Style ParagraphStyle(string id, string name, string size, string color, bool bold, string before, string after, bool keepNext = false, int? outline = null)
        {
            var paragraphProperties = new W.StyleParagraphProperties();
            if (keepNext)
                paragraphProperties.Append(new W.KeepNext());
            paragraphProperties.Append(new W.SpacingBetweenLines { Before = before, After = after });
            if (outline.HasValue)
                paragraphProperties.Append(new W.OutlineLevel { Val = outline.Value });

            var runProperties = new W.StyleRunProperties();
            if (bold)
                runProperties.Append(new W.Bold());
            if (color != null)
                runProperties.Append(new W.Color { Val = color });
            runProperties.Append(new W.FontSize { Val = size });

            return new W.Style(
                new W.StyleName { Val = name },
                new W.BasedOn { Val = "Normal" },
                new W.NextParagraphStyle { Val = "Normal" },
                new W.PrimaryStyle(),
                paragraphProperties,
                runProperties) { Type = W.StyleValues.Paragraph, StyleId = id };
        }

        var tableBorders = new W.TableBorders(
            new W.TopBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" },
            new W.LeftBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" },
            new W.BottomBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" },
            new W.RightBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" },
            new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" },
            new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4U, Color = "A6A6A6" });

        return new W.Styles(
            new W.DocDefaults(
                new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                    new W.RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri", EastAsia = "Calibri" },
                    new W.FontSize { Val = "22" })),
                new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                    new W.SpacingBetweenLines { After = "120", Line = "264", LineRule = W.LineSpacingRuleValues.Auto }))),
            new W.Style(new W.StyleName { Val = "Normal" }, new W.PrimaryStyle()) { Type = W.StyleValues.Paragraph, StyleId = "Normal", Default = true },
            ParagraphStyle("Title", "Title", "48", Navy, true, "0", "60"),
            ParagraphStyle("Subtitle", "Subtitle", "24", "595959", false, "0", "120"),
            ParagraphStyle("Heading1", "heading 1", "30", Blue, true, "320", "120", keepNext: true, outline: 0),
            ParagraphStyle("Meta", "Document Meta", "18", "7F7F7F", false, "0", "60"),
            ParagraphStyle("ListParagraph", "List Paragraph", "22", null, false, "0", "60"),
            new W.Style(
                new W.StyleName { Val = "Table Grid" },
                new W.StyleTableProperties(tableBorders)) { Type = W.StyleValues.Table, StyleId = "TableGrid" });
    }

    private static W.Numbering BulletNumbering() => new(
        new W.AbstractNum(
            new W.Level(
                new W.NumberingFormat { Val = W.NumberFormatValues.Bullet },
                new W.LevelText { Val = "•" },
                new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                new W.PreviousParagraphProperties(new W.Indentation { Left = "720", Hanging = "360" })) { LevelIndex = 0 }) { AbstractNumberId = 1 },
        new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 1 });

    #endregion

    #region Excel

    public byte[] GenerateExcelDocument(DocumentSpec about, DataTable table)
    {
        logger.LogInformation("Generating Excel workbook: {Title}", about.Title);

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = about.Title;
        workbook.Properties.Author = about.Author.FullName;
        workbook.Properties.LastModifiedBy = about.Author.FullName;
        workbook.Properties.Company = about.Organization;
        workbook.Properties.Created = about.Date;
        workbook.Properties.Modified = about.Date;

        var sheet = workbook.Worksheets.Add(SheetName(table.Name));
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = table.Columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#" + HeaderFill);
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var rowNumber = r + 2;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var cell = sheet.Cell(rowNumber, c + 1);
                var currency = table.CurrencyColumns.Contains(table.Columns[c]);
                switch (table.Rows[r][c])
                {
                    case Computed computed:
                        cell.FormulaA1 = computed.Formula.Replace("{r}", rowNumber.ToString());
                        break;
                    case decimal d:
                        cell.Value = d;
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                        break;
                    case var other:
                        cell.Value = other?.ToString() ?? string.Empty;
                        break;
                }
                if (currency)
                    cell.Style.NumberFormat.Format = "$#,##0.00";
            }
        }

        if (table.HasTotals && table.Rows.Count > 0)
        {
            var totalRow = table.Rows.Count + 2;
            sheet.Cell(totalRow, 1).Value = "Total";
            sheet.Row(totalRow).Style.Font.Bold = true;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                if (!table.CurrencyColumns.Contains(table.Columns[c]))
                    continue;
                var letter = XLHelper.GetColumnLetterFromNumber(c + 1);
                var cell = sheet.Cell(totalRow, c + 1);
                cell.FormulaA1 = $"SUM({letter}2:{letter}{totalRow - 1})";
                cell.Style.NumberFormat.Format = "$#,##0.00";
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, table.Rows.Count + 1), table.Columns.Count).SetAutoFilter();
        sheet.Columns().AdjustToContents(1, Math.Max(1, table.Rows.Count + 1), 8, 60);

        var info = workbook.Worksheets.Add("About");
        (string Label, object Value)[] rows =
        [
            ("Title", about.Title), ("Organization", about.Organization), ("Prepared by", $"{about.Author.FullName}, {about.Author.Title}"),
            ("Department", about.Author.Department), ("As of", about.Date), ("Classification", about.Classification)
        ];
        for (var i = 0; i < rows.Length; i++)
        {
            info.Cell(i + 1, 1).Value = rows[i].Label;
            info.Cell(i + 1, 1).Style.Font.Bold = true;
            if (rows[i].Value is DateTime date)
            {
                info.Cell(i + 1, 2).Value = date;
                info.Cell(i + 1, 2).Style.DateFormat.Format = "mmmm d, yyyy";
            }
            else
            {
                info.Cell(i + 1, 2).Value = rows[i].Value.ToString();
            }
        }
        info.Columns().AdjustToContents();

        // store computed values too, for readers that do not recalculate on open
        workbook.RecalculateAllFormulas();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SheetName(string name)
    {
        var clean = new string((name ?? "Sheet1").Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        return clean.Length switch { 0 => "Sheet1", > 31 => clean[..31], _ => clean };
    }

    #endregion

    #region PowerPoint

    private const long SlideWidth = 12192000;
    private const long SlideHeight = 6858000;
    private const int MaxBullets = 6;

    public byte[] GeneratePowerPointDocument(DocumentSpec spec)
    {
        logger.LogInformation("Generating PowerPoint presentation: {Title}", spec.Title);

        using var stream = new MemoryStream();
        // as with Word, the package is complete only once it is disposed
        using (var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            SetProperties(document, spec, "Microsoft Office PowerPoint");

            var presentationPart = document.AddPresentationPart();
            var masterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>("rId1");
            layoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(EmptyShapeTree()) { Name = "Blank" },
                new P.ColorMapOverride(new A.MasterColorMapping())) { Type = P.SlideLayoutValues.Blank, Preserve = true };
            layoutPart.AddPart(masterPart, "rId1");

            masterPart.AddNewPart<ThemePart>("rId2").Theme = Theme();
            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(EmptyShapeTree()),
                new P.ColorMap
                {
                    Background1 = A.ColorSchemeIndexValues.Light1, Text1 = A.ColorSchemeIndexValues.Dark1,
                    Background2 = A.ColorSchemeIndexValues.Light2, Text2 = A.ColorSchemeIndexValues.Dark2,
                    Accent1 = A.ColorSchemeIndexValues.Accent1, Accent2 = A.ColorSchemeIndexValues.Accent2,
                    Accent3 = A.ColorSchemeIndexValues.Accent3, Accent4 = A.ColorSchemeIndexValues.Accent4,
                    Accent5 = A.ColorSchemeIndexValues.Accent5, Accent6 = A.ColorSchemeIndexValues.Accent6,
                    Hyperlink = A.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink
                },
                new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = "rId1" }));

            var slides = new List<(string Title, List<string> Lines, bool Cover)>
            {
                (spec.Title, [spec.Subtitle, $"{spec.Author.FullName}, {spec.Author.Title}", spec.Date.ToString("MMMM d, yyyy")], true)
            };
            slides.AddRange(spec.Sections.Select(section => (section.Heading, SlideLines(section), false)));

            var slideIds = new P.SlideIdList();
            for (var i = 0; i < slides.Count; i++)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>($"rId{i + 10}");
                slidePart.Slide = Slide(slides[i].Title, slides[i].Lines, slides[i].Cover, $"{spec.Organization} | {spec.Classification}", i + 1);
                slidePart.AddPart(layoutPart);
                slideIds.Append(new P.SlideId { Id = (uint)(256 + i), RelationshipId = $"rId{i + 10}" });
            }

            presentationPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" }),
                slideIds,
                new P.SlideSize { Cx = (int)SlideWidth, Cy = (int)SlideHeight },
                new P.NotesSize { Cx = 6858000, Cy = 9144000 });
        }

        return stream.ToArray();
    }

    /// <summary>A section as slide bullets: its bullets, else the first sentence of each paragraph, then table rows.</summary>
    private static List<string> SlideLines(DocumentSection section)
    {
        var lines = new List<string>(section.Bullets);
        if (lines.Count == 0)
            lines.AddRange(section.Paragraphs.Select(FirstSentence));
        if (section.Table != null)
            lines.AddRange(section.Table.Rows.Select(row => string.Join(" | ", row.Take(3).Select(DataTable.Format))));
        return lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(MaxBullets).Select(l => Clip(l, 140)).ToList();
    }

    private static string FirstSentence(string text)
    {
        var end = text.IndexOf(". ", StringComparison.Ordinal);
        return end > 0 ? text[..(end + 1)] : text;
    }

    private static string Clip(string text, int length)
    {
        if (text.Length <= length)
            return text;
        var cut = text.LastIndexOf(' ', length);
        return text[..(cut > 0 ? cut : length)] + "…";
    }

    private static P.ShapeTree EmptyShapeTree() => new(
        new P.NonVisualGroupShapeProperties(
            new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
            new P.NonVisualGroupShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.GroupShapeProperties(new A.TransformGroup()));

    private static P.Slide Slide(string title, List<string> lines, bool cover, string footer, int number)
    {
        var tree = EmptyShapeTree();
        if (cover)
        {
            tree.Append(Band(2U, 0, 0, SlideWidth, SlideHeight, Navy));
            tree.Append(TextBox(3U, "Title", 838200, 2130000, 10515600, 1300000, [title], 4000, true, "FFFFFF", false));
            tree.Append(TextBox(4U, "Subtitle", 838200, 3500000, 10515600, 1600000, lines, 1800, false, "D9E2F3", false));
        }
        else
        {
            tree.Append(Band(2U, 0, 0, SlideWidth, 120000, Navy));
            tree.Append(TextBox(3U, "Title", 838200, 365125, 10515600, 1000000, [title], 3200, true, Navy, false));
            tree.Append(TextBox(4U, "Content", 838200, 1500000, 10515600, 4400000, lines, 2000, false, "262626", true));
            tree.Append(TextBox(5U, "Footer", 838200, 6200000, 9000000, 400000, [footer], 1000, false, "7F7F7F", false));
            tree.Append(TextBox(6U, "Slide Number", 10353800, 6200000, 1000000, 400000, [number.ToString()], 1000, false, "7F7F7F", false));
        }

        return new P.Slide(new P.CommonSlideData(tree), new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static P.Shape Band(uint id, long x, long y, long width, long height, string color) => new(
        new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties { Id = id, Name = $"Band {id}" },
            new P.NonVisualShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.ShapeProperties(
            new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = width, Cy = height }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
            new A.SolidFill(new A.RgbColorModelHex { Val = color }),
            new A.Outline(new A.NoFill())),
        new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));

    private static P.Shape TextBox(uint id, string name, long x, long y, long width, long height, List<string> lines,
        int size, bool bold, string color, bool bullets)
    {
        var body = new P.TextBody(new A.BodyProperties { Wrap = A.TextWrappingValues.Square }, new A.ListStyle());
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var paragraphProperties = bullets
                ? new A.ParagraphProperties(
                    new A.SpaceBefore(new A.SpacingPoints { Val = 600 }),
                    new A.BulletFont { Typeface = "Arial" },
                    new A.CharacterBullet { Char = "•" }) { LeftMargin = 342900, Indent = -342900 }
                : new A.ParagraphProperties();
            body.Append(new A.Paragraph(
                paragraphProperties,
                new A.Run(
                    new A.RunProperties(new A.SolidFill(new A.RgbColorModelHex { Val = color })) { Language = "en-US", FontSize = size, Bold = bold, Dirty = false },
                    new A.Text(line))));
        }
        if (!body.Elements<A.Paragraph>().Any())
            body.Append(new A.Paragraph());

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties { TextBox = true },
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                new A.NoFill()),
            body);
    }

    private static A.Theme Theme()
    {
        A.SolidFill SchemeFill() => new(new A.SchemeColor { Val = A.SchemeColorValues.PhColor });
        A.Outline Line(int width) => new(SchemeFill()) { Width = width };
        A.EffectStyle Effect() => new(new A.EffectList());

        return new A.Theme(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                    new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = "44546A" }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = "E7E6E6" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = "4472C4" }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = "ED7D31" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "A5A5A5" }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = "FFC000" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "5B9BD5" }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = "70AD47" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0563C1" }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "954F72" })) { Name = "Office" },
                new A.FontScheme(
                    new A.MajorFont(new A.LatinFont { Typeface = "Calibri Light" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                    new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" })) { Name = "Office" },
                new A.FormatScheme(
                    new A.FillStyleList(SchemeFill(), SchemeFill(), SchemeFill()),
                    new A.LineStyleList(Line(6350), Line(12700), Line(19050)),
                    new A.EffectStyleList(Effect(), Effect(), Effect()),
                    new A.BackgroundFillStyleList(SchemeFill(), SchemeFill(), SchemeFill())) { Name = "Office" }),
            new A.ObjectDefaults(),
            new A.ExtraColorSchemeList()) { Name = "Office Theme" };
    }

    #endregion

    private static void SetProperties(OpenXmlPackage package, DocumentSpec spec, string application)
    {
        package.PackageProperties.Title = spec.Title;
        package.PackageProperties.Subject = spec.Subtitle;
        package.PackageProperties.Creator = spec.Author.FullName;
        package.PackageProperties.LastModifiedBy = spec.Author.FullName;
        package.PackageProperties.Created = spec.Date;
        package.PackageProperties.Modified = spec.Date;

        var extended = package switch
        {
            WordprocessingDocument word => word.AddExtendedFilePropertiesPart(),
            PresentationDocument presentation => presentation.AddExtendedFilePropertiesPart(),
            _ => null
        };
        if (extended != null)
            extended.Properties = new X.Properties(new X.Company(spec.Organization), new X.Application(application));
    }
}
