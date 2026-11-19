namespace Ghosts.Pandora.Infrastructure.Content;

/// <summary>A format-neutral document. The Word, PDF, PowerPoint and text renderers all draw this.</summary>
public class DocumentSpec
{
    public string Title { get; init; }
    public string Subtitle { get; init; }
    public string Organization { get; init; }
    public Person Author { get; init; }
    public DateTime Date { get; init; }
    public string Classification { get; init; } = "Internal";
    public List<DocumentSection> Sections { get; } = [];
}

public class DocumentSection
{
    public string Heading { get; init; }
    public List<string> Paragraphs { get; } = [];
    public List<string> Bullets { get; } = [];
    public DataTable Table { get; set; }
}

/// <summary>A cell computed from others: its formula goes into spreadsheets, its value into CSV, JSON and documents.</summary>
public record Computed(decimal Value, string Formula);

/// <summary>
/// Typed tabular data. Cells are string, int, decimal, DateTime or <see cref="Computed"/>.
/// Formulas use {r} for the cell's own spreadsheet row.
/// </summary>
public class DataTable
{
    public string Name { get; init; }
    public List<string> Columns { get; init; } = [];
    public List<object[]> Rows { get; } = [];

    /// <summary>Columns that are money, so spreadsheets format and total them.</summary>
    public HashSet<string> CurrencyColumns { get; init; } = [];

    public bool HasTotals { get; init; }

    public static string Format(object value) => value switch
    {
        null => string.Empty,
        Computed c => c.Value.ToString("N2"),
        decimal d => d.ToString("N2"),
        DateTime dt => dt.ToString("yyyy-MM-dd"),
        _ => value.ToString()
    };

    public static object Plain(object value) => value is Computed c ? c.Value : value;
}
