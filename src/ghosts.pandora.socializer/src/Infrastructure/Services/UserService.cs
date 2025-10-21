using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IUserService
{
    Task<List<User>> GetAllUsersAsync();
    Task<User> GetUserByIdAsync(string username);
    Task<User> GetUserByUsernameAsync(string username);
    Task<User> CreateUserAsync(string username, string bio = null);
    Task<User> GetOrCreateUserAsync(string username, string bio = null);
    Task<User> UpdateUserAsync(string username, string bio = null, string status = null, string theme = null);
    Task<bool> DeleteUserAsync(string username);
    Task<bool> UsernameExistsAsync(string username);
    Task<List<User>> SearchUsersAsync(string searchTerm, int limit = 20);
}

public class UserService(DataContext context) : IUserService
{
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User> GetUserByIdAsync(string username)
    {
        return await context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User> GetUserByUsernameAsync(string username)
    {
        return await context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User> CreateUserAsync(string username, string bio = null)
    {
        if (await UsernameExistsAsync(username))
        {
            return await GetUserByUsernameAsync(username);
        }

        var user = new User
        {
            Username = username,
            Bio = bio ?? $"User {username}",
            Avatar = $"/u/{username}/avatar",
            CreatedUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            Status = "Active"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<User> GetOrCreateUserAsync(string username, string bio = null)
    {
        var existingUser = await GetUserByUsernameAsync(username);
        if (existingUser != null)
        {
            return existingUser;
        }

        return await CreateUserAsync(username, bio);
    }

    public async Task<User> UpdateUserAsync(string username, string bio = null, string status = null, string theme = null)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
            return null;

        if (bio != null)
            user.Bio = bio;

        if (status != null)
            user.Status = status;

        if (theme != null)
            user.Theme = theme;

        user.LastActiveUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeleteUserAsync(string username)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null)
            return false;

        context.Users.Remove(user);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm, int limit = 20)
    {
        return await context.Users
            .Where(u => u.Username.ToLower().Contains(searchTerm.ToLower()))
            .Take(limit)
            .ToListAsync();
    }
}
