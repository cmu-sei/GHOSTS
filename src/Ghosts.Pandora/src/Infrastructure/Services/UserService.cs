using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IUserService
{
    Task<List<User>> GetAllUsersAsync();
    Task<User> GetUserByIdAsync(Guid id);
    Task<User> GetUserByUsernameAsync(string username, string theme = null);
    Task<User> CreateUserAsync(string username, string theme, string bio = null);
    Task<User> GetOrCreateUserAsync(string username, string theme, string bio = null);
    Task<User> UpdateUserAsync(string username, string theme = null, string bio = null, string status = null, string newTheme = null);
    Task<bool> DeleteUserAsync(string username, string theme = null);
    Task<bool> UsernameExistsAsync(string username, string theme = null);
    Task<List<User>> SearchUsersAsync(string searchTerm, string theme = null, int limit = 20);
}

public class UserService(DataContext context) : IUserService
{
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User> GetUserByIdAsync(Guid id)
    {
        return await context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User> GetUserByUsernameAsync(string username, string theme = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var query = context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .Where(u => u.Username.ToLower() == normalizedUsername);

        var normalizedTheme = NormalizeThemeKey(theme);
        if (!string.IsNullOrWhiteSpace(normalizedTheme))
        {
            query = query.Where(u => u.Theme.ToLower() == normalizedTheme);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<User> CreateUserAsync(string username, string theme, string bio = null)
    {
        var normalizedTheme = NormalizeTheme(theme);

        if (await UsernameExistsAsync(username, normalizedTheme))
        {
            return await GetUserByUsernameAsync(username, normalizedTheme);
        }

        var user = new User
        {
            Username = username,
            Bio = bio ?? $"User {username}",
            Avatar = $"/u/{username}/avatar",
            Theme = normalizedTheme,
            CreatedUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            Status = "Active"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<User> GetOrCreateUserAsync(string username, string theme, string bio = null)
    {
        var normalizedTheme = NormalizeTheme(theme);
        var existingUser = await GetUserByUsernameAsync(username, normalizedTheme);
        if (existingUser != null)
        {
            return existingUser;
        }

        return await CreateUserAsync(username, normalizedTheme, bio);
    }

    public async Task<User> UpdateUserAsync(string username, string theme = null, string bio = null, string status = null, string newTheme = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var query = context.Users.Where(u => u.Username.ToLower() == normalizedUsername);

        var normalizedTheme = NormalizeThemeKey(theme);
        if (!string.IsNullOrWhiteSpace(normalizedTheme))
        {
            query = query.Where(u => u.Theme.ToLower() == normalizedTheme);
        }

        var user = await query.FirstOrDefaultAsync();
        if (user == null)
            return null;

        if (bio != null)
            user.Bio = bio;

        if (status != null)
            user.Status = status;

        if (newTheme != null)
            user.Theme = NormalizeTheme(newTheme);

        user.LastActiveUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeleteUserAsync(string username, string theme = null)
    {
        var user = await GetUserByUsernameAsync(username, theme);
        if (user == null)
            return false;

        context.Users.Remove(user);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UsernameExistsAsync(string username, string theme = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var query = context.Users.Where(u => u.Username.ToLower() == normalizedUsername);

        var normalizedTheme = NormalizeThemeKey(theme);
        if (!string.IsNullOrWhiteSpace(normalizedTheme))
        {
            query = query.Where(u => u.Theme.ToLower() == normalizedTheme);
        }

        return await query.AnyAsync();
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm, string theme = null, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<User>();
        }

        var normalizedTerm = searchTerm.Trim().ToLowerInvariant();
        var normalizedTheme = NormalizeThemeKey(theme);
        return await context.Users
            .Where(u => u.Username.ToLower().Contains(normalizedTerm))
            .Where(u => string.IsNullOrWhiteSpace(normalizedTheme) ||
                        u.Theme.ToLower() == normalizedTheme)
            .Take(limit)
            .ToListAsync();
    }

    private static string NormalizeTheme(string theme)
    {
        return string.IsNullOrWhiteSpace(theme) ? "default" : theme.Trim();
    }

    private static string NormalizeThemeKey(string theme)
    {
        return string.IsNullOrWhiteSpace(theme) ? null : theme.Trim().ToLowerInvariant();
    }
}
