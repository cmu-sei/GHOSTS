using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

public interface IThemeService
{
    Task<List<Theme>> GetAllThemesAsync();
    Task<List<Theme>> GetActiveThemesAsync();
    Task<Theme> GetThemeByIdAsync(int themeId);
    Task<Theme> GetThemeByNameAsync(string themeName);
    Task<Theme> CreateThemeAsync(string name, string displayName, string description = null);
    Task<Theme> UpdateThemeAsync(int themeId, string displayName = null, string description = null, bool? isActive = null);
    Task<bool> DeleteThemeAsync(int themeId);
    Task<bool> ThemeExistsAsync(string themeName);
}

public class ThemeService : IThemeService
{
    private readonly DataContext _context;

    public ThemeService(DataContext context)
    {
        _context = context;
    }

    public async Task<List<Theme>> GetAllThemesAsync()
    {
        return await _context.Themes
            .OrderBy(t => t.DisplayName)
            .ToListAsync();
    }

    public async Task<List<Theme>> GetActiveThemesAsync()
    {
        return await _context.Themes
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayName)
            .ToListAsync();
    }

    public async Task<Theme> GetThemeByIdAsync(int themeId)
    {
        return await _context.Themes
            .Include(t => t.Posts)
            .FirstOrDefaultAsync(t => t.Id == themeId);
    }

    public async Task<Theme> GetThemeByNameAsync(string themeName)
    {
        return await _context.Themes
            .Include(t => t.Posts)
            .FirstOrDefaultAsync(t => t.Name.ToLower() == themeName.ToLower());
    }

    public async Task<Theme> CreateThemeAsync(string name, string displayName, string description = null)
    {
        if (await ThemeExistsAsync(name))
        {
            throw new InvalidOperationException($"Theme '{name}' already exists.");
        }

        var theme = new Theme
        {
            Name = name.ToLower(),
            DisplayName = displayName,
            Description = description,
            IsActive = true
        };

        _context.Themes.Add(theme);
        await _context.SaveChangesAsync();

        return theme;
    }

    public async Task<Theme> UpdateThemeAsync(int themeId, string displayName = null, string description = null, bool? isActive = null)
    {
        var theme = await _context.Themes.FindAsync(themeId);
        if (theme == null)
            return null;

        if (displayName != null)
            theme.DisplayName = displayName;

        if (description != null)
            theme.Description = description;

        if (isActive.HasValue)
            theme.IsActive = isActive.Value;

        await _context.SaveChangesAsync();
        return theme;
    }

    public async Task<bool> DeleteThemeAsync(int themeId)
    {
        var theme = await _context.Themes.FindAsync(themeId);
        if (theme == null)
            return false;

        _context.Themes.Remove(theme);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ThemeExistsAsync(string themeName)
    {
        return await _context.Themes
            .AnyAsync(t => t.Name.ToLower() == themeName.ToLower());
    }
}
