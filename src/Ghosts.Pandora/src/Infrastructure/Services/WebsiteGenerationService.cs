using System.Text;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IWebsiteGenerationService
{
    string GenerateHomepage(string siteType, string siteName, int articleCount);
}

public class WebsiteGenerationService : IWebsiteGenerationService
{
    private readonly ILogger<WebsiteGenerationService> _logger;
    private static readonly Random Random = new();

    public WebsiteGenerationService(ILogger<WebsiteGenerationService> logger)
    {
        _logger = logger;
    }

    public string GenerateHomepage(string siteType, string siteName, int articleCount)
    {
        _logger.LogInformation("Generating {SiteType} website homepage: {SiteName}", siteType, siteName);

        return siteType.ToLower() switch
        {
            "news" => GenerateNewsHomepage(siteName, articleCount),
            "shopping" or "ecommerce" => GenerateShoppingHomepage(siteName, articleCount),
            "sports" => GenerateSportsHomepage(siteName, articleCount),
            "entertainment" => GenerateEntertainmentHomepage(siteName, articleCount),
            _ => GenerateNewsHomepage(siteName, articleCount)
        };
    }

    private string GenerateNewsHomepage(string siteName, int articleCount)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{siteName} - Breaking News, World News & Multimedia</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"/css/main.css\">");
        html.AppendLine("    <style>");
        html.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }");
        html.AppendLine("        body { font-family: 'Helvetica Neue', Arial, sans-serif; background: #fff; color: #333; }");
        html.AppendLine("        .header { background: #222; color: white; padding: 10px 0; border-bottom: 3px solid #c00; }");
        html.AppendLine("        .header-content { max-width: 1200px; margin: 0 auto; padding: 0 20px; }");
        html.AppendLine("        .logo { font-size: 32px; font-weight: bold; color: #c00; }");
        html.AppendLine("        .nav { background: #333; border-bottom: 1px solid #444; }");
        html.AppendLine("        .nav-content { max-width: 1200px; margin: 0 auto; padding: 0 20px; display: flex; gap: 30px; }");
        html.AppendLine("        .nav a { color: white; text-decoration: none; padding: 15px 0; display: inline-block; font-size: 14px; font-weight: 500; }");
        html.AppendLine("        .nav a:hover { color: #c00; }");
        html.AppendLine("        .container { max-width: 1200px; margin: 20px auto; padding: 0 20px; }");
        html.AppendLine("        .main-grid { display: grid; grid-template-columns: 2fr 1fr; gap: 30px; }");
        html.AppendLine("        .featured { grid-column: 1 / -1; background: #000; color: white; padding: 0; position: relative; height: 400px; overflow: hidden; }");
        html.AppendLine("        .featured img { width: 100%; height: 100%; object-fit: cover; }");
        html.AppendLine("        .featured-text { position: absolute; bottom: 0; left: 0; right: 0; background: linear-gradient(transparent, rgba(0,0,0,0.9)); padding: 40px; }");
        html.AppendLine("        .featured h1 { font-size: 36px; margin-bottom: 10px; }");
        html.AppendLine("        .featured .meta { color: #ccc; font-size: 14px; }");
        html.AppendLine("        .article-grid { display: grid; gap: 20px; }");
        html.AppendLine("        .article { border-bottom: 1px solid #ddd; padding-bottom: 20px; }");
        html.AppendLine("        .article img { width: 100%; height: 200px; object-fit: cover; margin-bottom: 10px; }");
        html.AppendLine("        .article h2 { font-size: 20px; margin-bottom: 10px; }");
        html.AppendLine("        .article h2 a { color: #333; text-decoration: none; }");
        html.AppendLine("        .article h2 a:hover { color: #c00; }");
        html.AppendLine("        .article .summary { color: #666; line-height: 1.6; margin-bottom: 10px; }");
        html.AppendLine("        .article .meta { color: #999; font-size: 12px; }");
        html.AppendLine("        .sidebar { }");
        html.AppendLine("        .sidebar-section { background: #f5f5f5; padding: 20px; margin-bottom: 20px; }");
        html.AppendLine("        .sidebar-section h3 { font-size: 18px; margin-bottom: 15px; border-bottom: 2px solid #c00; padding-bottom: 10px; }");
        html.AppendLine("        .sidebar-list { list-style: none; }");
        html.AppendLine("        .sidebar-list li { padding: 10px 0; border-bottom: 1px solid #ddd; }");
        html.AppendLine("        .sidebar-list li:last-child { border-bottom: none; }");
        html.AppendLine("        .sidebar-list a { color: #333; text-decoration: none; font-size: 14px; }");
        html.AppendLine("        .sidebar-list a:hover { color: #c00; }");
        html.AppendLine("        .footer { background: #222; color: #999; padding: 40px 20px; margin-top: 40px; }");
        html.AppendLine("        .footer-content { max-width: 1200px; margin: 0 auto; text-align: center; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Header
        html.AppendLine("    <div class=\"header\">");
        html.AppendLine("        <div class=\"header-content\">");
        html.AppendLine($"            <div class=\"logo\">{siteName}</div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        // Navigation
        html.AppendLine("    <div class=\"nav\">");
        html.AppendLine("        <div class=\"nav-content\">");
        var sections = new[] { "World", "Politics", "Business", "Technology", "Science", "Health", "Sports", "Entertainment" };
        foreach (var section in sections)
        {
            html.AppendLine($"            <a href=\"/{section.ToLower()}\">{section}</a>");
        }
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        html.AppendLine("    <div class=\"container\">");
        html.AppendLine("        <div class=\"main-grid\">");

        // Featured article
        var featuredHeadline = Faker.Lorem.Sentence(Random.Next(8, 15)).TrimEnd('.');
        html.AppendLine("            <div class=\"featured\">");
        html.AppendLine($"                <img src=\"/img/news/featured-{Random.Next(1000, 9999)}.jpg\" alt=\"Featured story\">");
        html.AppendLine("                <div class=\"featured-text\">");
        html.AppendLine($"                    <h1><a href=\"/article/{Random.Next(10000, 99999)}\" style=\"color: white; text-decoration: none;\">{featuredHeadline}</a></h1>");
        html.AppendLine($"                    <div class=\"meta\">{Faker.Name.FullName()} • {Random.Next(1, 6)} hours ago</div>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");

        // Main articles
        html.AppendLine("            <div class=\"article-grid\">");
        for (int i = 0; i < articleCount - 1; i++)
        {
            var headline = Faker.Lorem.Sentence(Random.Next(6, 12)).TrimEnd('.');
            var summary = Faker.Lorem.Paragraph(Random.Next(2, 4));
            var category = sections[Random.Next(sections.Length)];

            html.AppendLine("                <div class=\"article\">");
            html.AppendLine($"                    <img src=\"/images/news/{category.ToLower()}-{Random.Next(100, 999)}.jpg\" alt=\"{headline}\">");
            html.AppendLine($"                    <h2><a href=\"/article/{Random.Next(10000, 99999)}\">{headline}</a></h2>");
            html.AppendLine($"                    <div class=\"summary\">{summary}</div>");
            html.AppendLine($"                    <div class=\"meta\">{category} • {Faker.Name.FullName()} • {Random.Next(1, 24)} hours ago</div>");
            html.AppendLine("                </div>");
        }
        html.AppendLine("            </div>");

        // Sidebar
        html.AppendLine("            <div class=\"sidebar\">");

        // Trending section
        html.AppendLine("                <div class=\"sidebar-section\">");
        html.AppendLine("                    <h3>Trending Now</h3>");
        html.AppendLine("                    <ul class=\"sidebar-list\">");
        for (int i = 0; i < 5; i++)
        {
            html.AppendLine($"                        <li><a href=\"/article/{Random.Next(10000, 99999)}\">{Faker.Lorem.Sentence(Random.Next(5, 10)).TrimEnd('.')}</a></li>");
        }
        html.AppendLine("                    </ul>");
        html.AppendLine("                </div>");

        // Most Read section
        html.AppendLine("                <div class=\"sidebar-section\">");
        html.AppendLine("                    <h3>Most Read</h3>");
        html.AppendLine("                    <ul class=\"sidebar-list\">");
        for (int i = 0; i < 5; i++)
        {
            html.AppendLine($"                        <li><a href=\"/article/{Random.Next(10000, 99999)}\">{Faker.Lorem.Sentence(Random.Next(5, 10)).TrimEnd('.')}</a></li>");
        }
        html.AppendLine("                    </ul>");
        html.AppendLine("                </div>");

        html.AppendLine("            </div>");

        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        // Footer
        html.AppendLine("    <div class=\"footer\">");
        html.AppendLine("        <div class=\"footer-content\">");
        html.AppendLine($"            <p>&copy; {DateTime.Now.Year} {siteName}. All Rights Reserved.</p>");
        html.AppendLine("            <p><a href=\"/about\" style=\"color: #999;\">About</a> | <a href=\"/contact\" style=\"color: #999;\">Contact</a> | <a href=\"/privacy\" style=\"color: #999;\">Privacy Policy</a> | <a href=\"/terms\" style=\"color: #999;\">Terms of Service</a></p>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        html.AppendLine("    <script src=\"/js/main.js\"></script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private string GenerateShoppingHomepage(string siteName, int productCount)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{siteName} - Online Shopping for Electronics, Apparel & More</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }");
        html.AppendLine("        body { font-family: Arial, sans-serif; background: #f5f5f5; }");
        html.AppendLine("        .header { background: #232f3e; color: white; padding: 15px 0; }");
        html.AppendLine("        .header-content { max-width: 1400px; margin: 0 auto; padding: 0 20px; display: flex; align-items: center; gap: 20px; }");
        html.AppendLine("        .logo { font-size: 28px; font-weight: bold; color: #ff9900; }");
        html.AppendLine("        .search { flex: 1; }");
        html.AppendLine("        .search input { width: 100%; padding: 10px; border: none; border-radius: 4px; }");
        html.AppendLine("        .nav { background: #37475a; padding: 10px 0; }");
        html.AppendLine("        .nav-content { max-width: 1400px; margin: 0 auto; padding: 0 20px; display: flex; gap: 20px; }");
        html.AppendLine("        .nav a { color: white; text-decoration: none; font-size: 14px; }");
        html.AppendLine("        .banner { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 60px 20px; text-align: center; }");
        html.AppendLine("        .banner h1 { font-size: 42px; margin-bottom: 10px; }");
        html.AppendLine("        .container { max-width: 1400px; margin: 20px auto; padding: 0 20px; }");
        html.AppendLine("        .section-title { font-size: 24px; margin: 30px 0 20px; }");
        html.AppendLine("        .product-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 20px; }");
        html.AppendLine("        .product { background: white; border-radius: 8px; padding: 15px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); transition: transform 0.2s; }");
        html.AppendLine("        .product:hover { transform: translateY(-5px); box-shadow: 0 4px 8px rgba(0,0,0,0.2); }");
        html.AppendLine("        .product img { width: 100%; height: 200px; object-fit: cover; border-radius: 4px; margin-bottom: 10px; }");
        html.AppendLine("        .product h3 { font-size: 16px; margin-bottom: 10px; height: 40px; overflow: hidden; }");
        html.AppendLine("        .product .price { color: #b12704; font-size: 20px; font-weight: bold; margin-bottom: 10px; }");
        html.AppendLine("        .product .rating { color: #ffa41c; margin-bottom: 10px; }");
        html.AppendLine("        .product button { background: #ff9900; border: none; color: #111; padding: 10px; width: 100%; border-radius: 4px; cursor: pointer; font-weight: bold; }");
        html.AppendLine("        .product button:hover { background: #fa8900; }");
        html.AppendLine("        .footer { background: #232f3e; color: white; padding: 40px 20px; margin-top: 40px; text-align: center; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Header
        html.AppendLine("    <div class=\"header\">");
        html.AppendLine("        <div class=\"header-content\">");
        html.AppendLine($"            <div class=\"logo\">{siteName}</div>");
        html.AppendLine("            <div class=\"search\"><input type=\"text\" placeholder=\"Search for products...\"></div>");
        html.AppendLine("            <div>Cart (0)</div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        // Navigation
        html.AppendLine("    <div class=\"nav\">");
        html.AppendLine("        <div class=\"nav-content\">");
        var categories = new[] { "Electronics", "Clothing", "Home & Garden", "Sports", "Toys", "Books", "Beauty" };
        foreach (var cat in categories)
        {
            html.AppendLine($"            <a href=\"/{cat.ToLower().Replace(" & ", "-")}\">{cat}</a>");
        }
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        // Banner
        html.AppendLine("    <div class=\"banner\">");
        html.AppendLine("        <h1>Summer Sale - Up to 50% Off</h1>");
        html.AppendLine("        <p>Limited time offers on thousands of items</p>");
        html.AppendLine("    </div>");

        html.AppendLine("    <div class=\"container\">");

        // Products
        html.AppendLine("        <h2 class=\"section-title\">Featured Products</h2>");
        html.AppendLine("        <div class=\"product-grid\">");
        for (int i = 0; i < productCount; i++)
        {
            var productName = Faker.Lorem.Sentence(Random.Next(3, 6)).TrimEnd('.');
            var price = Random.Next(10, 500);
            var rating = Random.Next(3, 5);

            html.AppendLine("            <div class=\"product\">");
            html.AppendLine($"                <img src=\"/images/products/product-{Random.Next(1000, 9999)}.jpg\" alt=\"{productName}\">");
            html.AppendLine($"                <h3>{productName}</h3>");
            html.AppendLine($"                <div class=\"price\">${price}.{Random.Next(0, 99):D2}</div>");
            html.AppendLine($"                <div class=\"rating\">{'★' * rating}{'☆' * (5 - rating)} ({Random.Next(10, 500)})</div>");
            html.AppendLine("                <button>Add to Cart</button>");
            html.AppendLine("            </div>");
        }
        html.AppendLine("        </div>");

        html.AppendLine("    </div>");

        // Footer
        html.AppendLine("    <div class=\"footer\">");
        html.AppendLine($"        <p>&copy; {DateTime.Now.Year} {siteName}. All Rights Reserved.</p>");
        html.AppendLine("    </div>");

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private string GenerateSportsHomepage(string siteName, int articleCount)
    {
        // Similar structure to news but with sports-specific styling
        return GenerateNewsHomepage(siteName.Replace("Daily Chronicle", "Sports Network"), articleCount)
            .Replace("Breaking News, World News", "Sports News, Scores & Highlights")
            .Replace("#c00", "#007acc");
    }

    private string GenerateEntertainmentHomepage(string siteName, int articleCount)
    {
        // Similar structure to news but with entertainment-specific styling
        return GenerateNewsHomepage(siteName.Replace("Daily Chronicle", "Entertainment Tonight"), articleCount)
            .Replace("Breaking News, World News", "Celebrity News, Movies & TV")
            .Replace("#c00", "#e50914");
    }
}
