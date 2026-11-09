using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Themes must be found from the compiled views: a published app has no view files on disk.
/// </summary>
public class ThemeServiceTests
{
    private static ThemeService CreateService()
    {
        var mvc = new ServiceCollection().AddControllersWithViews().AddApplicationPart(typeof(ThemeService).Assembly);
        return new ThemeService(mvc.PartManager);
    }

    [Fact]
    public void GetAvailableThemes_ListsThemeViewFolders()
    {
        var themes = CreateService().GetAvailableThemes();

        Assert.Contains("default", themes);
        Assert.Contains("linkedin", themes);
    }

    [Theory]
    [InlineData("linkedin", true)]
    [InlineData("nosuchtheme", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ThemeExists_MatchesThemeViewFolders(string? theme, bool expected)
    {
        Assert.Equal(expected, CreateService().ThemeExists(theme!));
    }
}
