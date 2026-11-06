using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;

namespace Ghosts.Pandora.Infrastructure.Services;

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
    private readonly List<string> _themes;

    public ThemeService(ApplicationPartManager partManager)
    {
        // themes are read from the compiled views, as a published app has no view files on disk
        var feature = new ViewsFeature();
        partManager.PopulateFeature(feature);
        _themes = feature.ViewDescriptors
            .Select(view => view.RelativePath.Split('/'))
            .Where(path => path.Length > 4 && path[1] == "Views" && path[2] == "Themes")
            .Select(path => path[3])
            .Distinct()
            .OrderBy(name => name)
            .ToList();
    }

    public List<string> GetAvailableThemes()
    {
        return _themes;
    }

    public bool ThemeExists(string themeName)
    {
        return _themes.Contains(themeName);
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
