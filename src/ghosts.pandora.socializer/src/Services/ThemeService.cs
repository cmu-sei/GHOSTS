using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

public interface IThemeService
{
    List<string> GetAvailableThemes();
    bool ThemeExists(string themeName);
    ThemeInfo GetThemeInfo(string themeName);
    Task<ThemeInfo> GetThemeByNameAsync(string themeName);
}

public class ThemeInfo
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ThemeService : IThemeService
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _themesPath;

    public ThemeService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _themesPath = Path.Combine(_environment.ContentRootPath, "Views", "Themes");
    }

    public List<string> GetAvailableThemes()
    {
        if (!Directory.Exists(_themesPath))
            return new List<string>();

        return Directory.GetDirectories(_themesPath)
            .Select(dir => Path.GetFileName(dir))
            .Where(themeName => !string.IsNullOrEmpty(themeName))
            .OrderBy(name => name)
            .ToList();
    }

    public bool ThemeExists(string themeName)
    {
        if (string.IsNullOrEmpty(themeName))
            return false;

        var themePath = Path.Combine(_themesPath, themeName);
        return Directory.Exists(themePath);
    }

    public ThemeInfo GetThemeInfo(string themeName)
    {
        if (!ThemeExists(themeName))
            return null;

        // Generate display names from theme names
        var displayName = themeName switch
        {
            "default" => "Default",
            "facebook" => "Facebook",
            "instagram" => "Instagram",
            "discord" => "Discord",
            "x" => "X (Twitter)",
            "linkedin" => "LinkedIn",
            "reddit" => "Reddit",
            "youtube" => "YouTube",
            _ => char.ToUpper(themeName[0]) + themeName.Substring(1)
        };

        var description = themeName switch
        {
            "default" => "Clean, minimal social platform theme",
            "facebook" => "Facebook-style social media theme",
            "instagram" => "Instagram-style photo sharing theme",
            "discord" => "Discord-style chat theme",
            "x" => "X/Twitter-style microblogging theme",
            "linkedin" => "LinkedIn-style professional networking theme",
            "reddit" => "Reddit-style forum theme",
            "youtube" => "YouTube-style video platform theme",
            _ => $"{displayName} theme"
        };

        return new ThemeInfo
        {
            Name = themeName,
            DisplayName = displayName,
            Description = description,
            IsActive = true
        };
    }

    public async Task<ThemeInfo> GetThemeByNameAsync(string themeName)
    {
        return await Task.FromResult(GetThemeInfo(themeName));
    }
}
