using Faker;
using SkiaSharp;

namespace Ghosts.Socializer.Infrastructure.Services;

public static class ContentGenerationHelper
{
    private static readonly Random Random = new();

    public static string GenerateRandomName(string extension = "")
    {
        return $"{Lorem.GetFirstWord()}_{Lorem.GetFirstWord()}{extension}";
    }

    public static byte[] GenerateRandomImage(string format = "png")
    {
        var width = Random.Next(200, 800);
        var height = Random.Next(200, 800);

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.White);

        for (var i = 0; i < Random.Next(5, 15); i++)
        {
            var paint = new SKPaint
            {
                Color = new SKColor(
                    (byte)Random.Next(256),
                    (byte)Random.Next(256),
                    (byte)Random.Next(256)
                ),
                IsAntialias = true
            };

            var shapeType = Random.Next(2);
            var x1 = Random.Next(width);
            var y1 = Random.Next(height);

            if (shapeType == 0)
            {
                var radius = Random.Next(10, 100);
                canvas.DrawCircle(x1, y1, radius, paint);
            }
            else
            {
                var x2 = Random.Next(x1 + 1, width);
                var y2 = Random.Next(y1 + 1, height);
                canvas.DrawRect(x1, y1, x2 - x1, y2 - y1, paint);
            }
        }

        var fontSize = Random.Next(20, 50);
        var font = new SKFont { Size = fontSize };
        var textPaint = new SKPaint
        {
            Color = new SKColor(
                (byte)Random.Next(256),
                (byte)Random.Next(256),
                (byte)Random.Next(256)
            ),
            IsAntialias = true
        };

        var text = Lorem.Sentence(Random.Next(2, 6));
        var textX = Random.Next(0, Math.Max(0, width - 100));
        var textY = Random.Next(fontSize, height);
        canvas.DrawText(text, textX, textY, font, textPaint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = format.ToLower() switch
        {
            "jpg" or "jpeg" => image.Encode(SKEncodedImageFormat.Jpeg, 90),
            "png" => image.Encode(SKEncodedImageFormat.Png, 90),
            "gif" => image.Encode(SKEncodedImageFormat.Gif, 90),
            _ => image.Encode(SKEncodedImageFormat.Png, 90)
        };

        return data.ToArray();
    }

    public static List<string> GenerateRandomSentences(int count)
    {
        var sentences = new List<string>();
        for (var i = 0; i < count; i++)
        {
            sentences.Add(Lorem.Sentence());
        }
        return sentences;
    }

    public static List<string> GenerateRandomParagraphs(int count)
    {
        var paragraphs = new List<string>();
        for (var i = 0; i < count; i++)
        {
            paragraphs.Add(Lorem.Paragraph());
        }
        return paragraphs;
    }

    public static string GenerateRandomTitle()
    {
        return Lorem.Sentence(Random.Next(3, 8)).TrimEnd('.');
    }

    public static Dictionary<string, object> GenerateRandomJsonData(int rows = 10)
    {
        var data = new List<Dictionary<string, object>>();

        for (var i = 0; i < rows; i++)
        {
            var row = new Dictionary<string, object>
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["name"] = Name.FullName(),
                ["email"] = Internet.Email(),
                ["company"] = Company.Name(),
                ["address"] = Address.StreetAddress(),
                ["city"] = Address.City(),
                ["country"] = Address.Country(),
                ["phone"] = Phone.Number(),
                ["created"] = DateTime.UtcNow.AddDays(-Random.Next(1, 365)).ToString("O")
            };
            data.Add(row);
        }

        return new Dictionary<string, object>
        {
            ["data"] = data,
            ["count"] = rows,
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        };
    }

    public static List<string[]> GenerateRandomCsvData(int rows = 10, int columns = 5)
    {
        var data = new List<string[]>();

        var headers = new string[columns];
        for (var i = 0; i < columns; i++)
        {
            headers[i] = Lorem.GetFirstWord().ToUpper();
        }
        data.Add(headers);

        for (var i = 0; i < rows; i++)
        {
            var row = new string[columns];
            for (var j = 0; j < columns; j++)
            {
                row[j] = Random.Next(2) == 0 ? Lorem.GetFirstWord() : Lorem.Sentence(1);
            }
            data.Add(row);
        }

        return data;
    }
}
