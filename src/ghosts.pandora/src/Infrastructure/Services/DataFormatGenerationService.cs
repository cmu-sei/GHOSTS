using System.Text;
using System.Text.Json;
using CsvHelper;
using System.Globalization;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IDataFormatGenerationService
{
    string GenerateJson(int rows = 10);
    byte[] GenerateCsv(int rows = 10, int columns = 5);
    string GenerateText(int paragraphs = 5);
    string GenerateHtml(string title = null);
    string GenerateScript();
    string GenerateStylesheet();
}

public class DataFormatGenerationService : IDataFormatGenerationService
{
    private readonly ILogger<DataFormatGenerationService> _logger;
    private static readonly Random Random = new();

    public DataFormatGenerationService(ILogger<DataFormatGenerationService> logger)
    {
        _logger = logger;
    }

    public string GenerateJson(int rows = 10)
    {
        _logger.LogInformation("Generating JSON with {Rows} rows", rows);
        var data = ContentGenerationHelper.GenerateRandomJsonData(rows);
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public byte[] GenerateCsv(int rows = 10, int columns = 5)
    {
        _logger.LogInformation("Generating CSV with {Rows} rows and {Columns} columns", rows, columns);

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        var data = ContentGenerationHelper.GenerateRandomCsvData(rows, columns);

        foreach (var row in data)
        {
            foreach (var field in row)
            {
                csv.WriteField(field);
            }
            csv.NextRecord();
        }

        writer.Flush();
        return memoryStream.ToArray();
    }

    public string GenerateText(int paragraphs = 5)
    {
        _logger.LogInformation("Generating text with {Paragraphs} paragraphs", paragraphs);
        var content = ContentGenerationHelper.GenerateRandomParagraphs(paragraphs);
        return string.Join("\n\n", content);
    }

    public string GenerateHtml(string title = null)
    {
        title ??= ContentGenerationHelper.GenerateRandomTitle();
        _logger.LogInformation("Generating HTML with title: {Title}", title);

        var paragraphCount = Random.Next(3, 8);
        var paragraphs = ContentGenerationHelper.GenerateRandomParagraphs(paragraphCount);
        var imageCount = Random.Next(1, 4);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{title}</title>");

        // Link to external CSS files that can be dynamically generated
        html.AppendLine($"    <link rel=\"stylesheet\" href=\"/css/styles-{Random.Next(1000, 9999)}.css\">");
        html.AppendLine($"    <link rel=\"stylesheet\" href=\"/css/theme-{Faker.Lorem.GetFirstWord().ToLower()}.css\">");

        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; max-width: 900px; margin: 50px auto; padding: 20px; background: #f5f5f5; }");
        html.AppendLine("        .container { background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("        h1 { color: #333; border-bottom: 3px solid #007bff; padding-bottom: 10px; }");
        html.AppendLine("        h2 { color: #555; margin-top: 30px; }");
        html.AppendLine("        p { line-height: 1.8; color: #666; margin-bottom: 20px; }");
        html.AppendLine("        img { max-width: 100%; height: auto; margin: 20px 0; border-radius: 4px; }");
        html.AppendLine("        .image-gallery { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 15px; margin: 20px 0; }");
        html.AppendLine("        nav { background: #007bff; padding: 15px; margin: -30px -30px 30px -30px; border-radius: 8px 8px 0 0; }");
        html.AppendLine("        nav a { color: white; text-decoration: none; margin-right: 20px; }");
        html.AppendLine("        footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #ddd; color: #999; text-align: center; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");

        // Navigation with links to other generated resources
        html.AppendLine("        <nav>");
        html.AppendLine($"            <a href=\"/\">Home</a>");
        html.AppendLine($"            <a href=\"/docs/{Faker.Lorem.GetFirstWord()}.html\">About</a>");
        html.AppendLine($"            <a href=\"/api/data.json\">Data</a>");
        html.AppendLine($"            <a href=\"/docs/{Faker.Lorem.GetFirstWord()}.pdf\">Reports</a>");
        html.AppendLine($"            <a href=\"/docs/contact.html\">Contact</a>");
        html.AppendLine("        </nav>");

        html.AppendLine($"        <h1>{title}</h1>");

        // Add some paragraphs with occasional images
        var imageInterval = Random.Next(2, 4); // Consistent interval for images
        for (int i = 0; i < paragraphs.Count; i++)
        {
            html.AppendLine($"        <p>{paragraphs[i]}</p>");

            // Add an image every imageInterval paragraphs
            if (i > 0 && i % imageInterval == 0 && imageCount > 0)
            {
                var imageType = new[] { "photo", "banner", "graphic", "thumbnail", "header" }[Random.Next(5)];
                html.AppendLine($"        <img src=\"/img/{imageType}-{Random.Next(100, 999)}.jpg\" alt=\"{Faker.Lorem.Sentence(3)}\" />");
                imageCount--;
            }
        }

        // Always add at least one image if none were added
        if (imageCount > 0)
        {
            var imageType = new[] { "photo", "banner", "graphic", "thumbnail", "header" }[Random.Next(5)];
            html.AppendLine($"        <img src=\"/img/featured-{imageType}-{Random.Next(100, 999)}.jpg\" alt=\"{Faker.Lorem.Sentence(3)}\" />");
        }

        // Add a section with links to downloadable resources
        if (Random.Next(2) == 0)
        {
            html.AppendLine("        <h2>Resources</h2>");
            html.AppendLine("        <ul>");
            html.AppendLine($"            <li><a href=\"/pdf/{Faker.Lorem.GetFirstWord()}-report.pdf\">Download Report (PDF)</a></li>");
            html.AppendLine($"            <li><a href=\"/xlsx/{Faker.Lorem.GetFirstWord()}-data.xlsx\">Download Data (Excel)</a></li>");
            html.AppendLine($"            <li><a href=\"/docs/{Faker.Lorem.GetFirstWord()}-document.docx\">Download Document (Word)</a></li>");
            html.AppendLine($"            <li><a href=\"/api/{Faker.Lorem.GetFirstWord()}.json\">View API Data (JSON)</a></li>");
            html.AppendLine("        </ul>");
        }

        // Always add an image gallery section for realism
        html.AppendLine("        <h2>Gallery</h2>");
        html.AppendLine("        <div class=\"image-gallery\">");
        for (int i = 0; i < Random.Next(3, 6); i++)
        {
            html.AppendLine($"            <img src=\"/images/gallery/{Faker.Lorem.GetFirstWord()}-{i}.png\" alt=\"Gallery image {i + 1}\" />");
        }
        html.AppendLine("        </div>");

        html.AppendLine("        <footer>");
        html.AppendLine($"            <p>&copy; {DateTime.Now.Year} {Faker.Company.Name()}. All rights reserved.</p>");
        html.AppendLine($"            <p><a href=\"/docs/privacy.html\">Privacy Policy</a> | <a href=\"/docs/terms.html\">Terms of Service</a></p>");
        html.AppendLine("        </footer>");
        html.AppendLine("    </div>");

        // Link to external JavaScript files that can be dynamically generated
        html.AppendLine($"    <script src=\"/js/main-{Random.Next(1000, 9999)}.js\"></script>");
        html.AppendLine($"    <script src=\"/js/{Faker.Lorem.GetFirstWord().ToLower()}-utils.js\"></script>");

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    public string GenerateScript()
    {
        _logger.LogInformation("Generating JavaScript");

        var functionName = Faker.Lorem.GetFirstWord().ToLower();
        var variableName = Faker.Lorem.GetFirstWord().ToLower();

        var script = new StringBuilder();
        script.AppendLine("(function() {");
        script.AppendLine($"    'use strict';");
        script.AppendLine();
        script.AppendLine($"    var {variableName} = '{Faker.Lorem.Sentence()}';");
        script.AppendLine();
        script.AppendLine($"    function {functionName}() {{");
        script.AppendLine($"        console.log({variableName});");
        script.AppendLine($"        return true;");
        script.AppendLine("    }");
        script.AppendLine();
        script.AppendLine($"    // Initialize");
        script.AppendLine($"    {functionName}();");
        script.AppendLine("})();");

        return script.ToString();
    }

    public string GenerateStylesheet()
    {
        _logger.LogInformation("Generating CSS");

        var className = Faker.Lorem.GetFirstWord().ToLower();
        var idName = Faker.Lorem.GetFirstWord().ToLower();

        var css = new StringBuilder();
        css.AppendLine("/* Generated Stylesheet */");
        css.AppendLine();
        css.AppendLine("body {");
        css.AppendLine("    font-family: 'Helvetica Neue', Arial, sans-serif;");
        css.AppendLine("    margin: 0;");
        css.AppendLine("    padding: 0;");
        css.AppendLine($"    background-color: #{Random.Next(0x1000000):X6};");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine($".{className} {{");
        css.AppendLine($"    color: #{Random.Next(0x1000000):X6};");
        css.AppendLine($"    font-size: {Random.Next(12, 24)}px;");
        css.AppendLine($"    padding: {Random.Next(10, 30)}px;");
        css.AppendLine("}");
        css.AppendLine();
        css.AppendLine($"#{idName} {{");
        css.AppendLine($"    margin: {Random.Next(10, 50)}px auto;");
        css.AppendLine($"    max-width: {Random.Next(600, 1200)}px;");
        css.AppendLine("}");

        return css.ToString();
    }
}
