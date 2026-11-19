using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

// FileUploadTests changes the current directory, so nothing may run beside it
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Hosts the real app against a throwaway SQLite database and content directory, with a fixed
/// world seed and date so generated content is the same on every run.
/// </summary>
public class PandoraFactory : WebApplicationFactory<Program>
{
    public const int Seed = 1234;

    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"pandora-tests-{Guid.NewGuid():N}");
    public string ContentDirectory => Path.Combine(Root, "_data");

    private readonly Dictionary<string, string> _settings;

    public PandoraFactory() : this(null)
    {
    }

    /// <summary>A factory with settings on top of the defaults (xUnit fixtures need the parameterless constructor).</summary>
    public static PandoraFactory With(Dictionary<string, string> settings) => new(settings);

    protected PandoraFactory(Dictionary<string, string> settings)
    {
        Directory.CreateDirectory(Root);
        _settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(Root, "pandora.db")}",
            ["Database:Provider"] = "SQLite",
            ["ApplicationConfiguration:Pandora:ContentCacheDirectory"] = ContentDirectory,
            ["ApplicationConfiguration:World:Seed"] = Seed.ToString(),
            ["ApplicationConfiguration:World:ReferenceDate"] = "2026-11-19",
            ["ApplicationConfiguration:Mode:Type"] = "social",
            ["ApplicationConfiguration:Mode:DefaultTheme"] = "facebook",
            ["ApplicationConfiguration:Ghosts:ApiUrl"] = ""
        };
        foreach (var (key, value) in settings ?? [])
            _settings[key] = value;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);
    }

    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
            // a leftover temp directory is not worth failing a test run over
        }
    }
}
