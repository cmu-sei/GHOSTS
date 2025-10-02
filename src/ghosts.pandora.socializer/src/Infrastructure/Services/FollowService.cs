using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IFollowService
{
    Task<bool> FollowAsync(string followerUsername, string followeeUsername);
    Task<bool> UnfollowAsync(string followerUsername, string followeeUsername);
    Task<bool> IsFollowingAsync(string followerUsername, string followeeUsername);
    Task<int> GetFollowerCountAsync(string username);
    Task<int> GetFollowingCountAsync(string username);
    Task<List<User>> GetFollowersAsync(string username, int limit = 50);
    Task<List<User>> GetFollowingAsync(string username, int limit = 50);
}

public class FollowService(DataContext context) : IFollowService
{
    public async Task<bool> FollowAsync(string followerUsername, string followeeUsername)
    {
        if (string.IsNullOrWhiteSpace(followerUsername) || string.IsNullOrWhiteSpace(followeeUsername))
            return false;

        if (string.Equals(followerUsername, followeeUsername, StringComparison.OrdinalIgnoreCase))
            return false;

        var exists = await context.Followers.AnyAsync(f =>
            f.FollowerUsername.ToLower() == followerUsername.ToLower() &&
            f.Username.ToLower() == followeeUsername.ToLower());

        if (exists)
            return false;

        var follow = new Followers
        {
            FollowerUsername = followerUsername,
            Username = followeeUsername,
            CreatedUtc = DateTime.UtcNow
        };

        context.Followers.Add(follow);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnfollowAsync(string followerUsername, string followeeUsername)
    {
        if (string.IsNullOrWhiteSpace(followerUsername) || string.IsNullOrWhiteSpace(followeeUsername))
            return false;

        var follow = await context.Followers.FirstOrDefaultAsync(f =>
            f.FollowerUsername.ToLower() == followerUsername.ToLower() &&
            f.Username.ToLower() == followeeUsername.ToLower());

        if (follow == null)
            return false;

        context.Followers.Remove(follow);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsFollowingAsync(string followerUsername, string followeeUsername)
    {
        if (string.IsNullOrWhiteSpace(followerUsername) || string.IsNullOrWhiteSpace(followeeUsername))
            return false;

        return await context.Followers.AnyAsync(f =>
            f.FollowerUsername.ToLower() == followerUsername.ToLower() &&
            f.Username.ToLower() == followeeUsername.ToLower());
    }

    public async Task<int> GetFollowerCountAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return 0;

        return await context.Followers.CountAsync(f =>
            f.Username.ToLower() == username.ToLower());
    }

    public async Task<int> GetFollowingCountAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return 0;

        return await context.Followers.CountAsync(f =>
            f.FollowerUsername.ToLower() == username.ToLower());
    }

    public async Task<List<User>> GetFollowersAsync(string username, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new List<User>();

        return await context.Followers
            .Where(f => f.Username.ToLower() == username.ToLower())
            .Include(f => f.Follower)
            .OrderByDescending(f => f.CreatedUtc)
            .Take(limit)
            .Select(f => f.Follower)
            .ToListAsync();
    }

    public async Task<List<User>> GetFollowingAsync(string username, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new List<User>();

        return await context.Followers
            .Where(f => f.FollowerUsername.ToLower() == username.ToLower())
            .Include(f => f.Followee)
            .OrderByDescending(f => f.CreatedUtc)
            .Take(limit)
            .Select(f => f.Followee)
            .ToListAsync();
    }
}
