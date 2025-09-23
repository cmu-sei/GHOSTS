using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

public interface IUserService
{
    Task<List<User>> GetAllUsersAsync();
    Task<User> GetUserByIdAsync(string userId);
    Task<User> GetUserByUsernameAsync(string username);
    Task<User> CreateUserAsync(string username, string displayName = null, string bio = null);
    Task<User> UpdateUserAsync(string userId, string displayName = null, string bio = null, string status = null);
    Task<bool> DeleteUserAsync(string userId);
    Task<bool> UsernameExistsAsync(string username);
    Task<List<User>> SearchUsersAsync(string searchTerm, int limit = 20);
}

public class UserService : IUserService
{
    private readonly DataContext _context;

    public UserService(DataContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User> GetUserByIdAsync(string userId)
    {
        return await _context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .Include(u => u.Posts)
            .Include(u => u.Likes)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<User> CreateUserAsync(string username, string displayName = null, string bio = null)
    {
        if (await UsernameExistsAsync(username))
        {
            throw new InvalidOperationException($"Username '{username}' already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            DisplayName = displayName ?? username,
            Bio = bio ?? $"User {username}",
            Avatar = $"/u/{username}/avatar",
            CreatedUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            Status = "Active"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User> UpdateUserAsync(string userId, string displayName = null, string bio = null, string status = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return null;

        if (displayName != null)
            user.DisplayName = displayName;

        if (bio != null)
            user.Bio = bio;

        if (status != null)
            user.Status = status;

        user.LastActiveUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm, int limit = 20)
    {
        return await _context.Users
            .Where(u => u.Username.ToLower().Contains(searchTerm.ToLower()) ||
                       u.DisplayName.ToLower().Contains(searchTerm.ToLower()))
            .Take(limit)
            .ToListAsync();
    }
}
