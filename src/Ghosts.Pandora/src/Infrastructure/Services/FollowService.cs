using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IFollowService
{
    Task<bool> FollowAsync(Guid followerUserId, Guid followeeUserId);
    Task<bool> UnfollowAsync(Guid followerUserId, Guid followeeUserId);
    Task<bool> IsFollowingAsync(Guid followerUserId, Guid followeeUserId);
    Task<int> GetFollowerCountAsync(Guid userId);
    Task<int> GetFollowingCountAsync(Guid userId);
    Task<List<User>> GetFollowersAsync(Guid userId, int limit = 50);
    Task<List<User>> GetFollowingAsync(Guid userId, int limit = 50);
}

public class FollowService(DataContext context) : IFollowService
{
    public async Task<bool> FollowAsync(Guid followerUserId, Guid followeeUserId)
    {
        if (followerUserId == Guid.Empty || followeeUserId == Guid.Empty)
            return false;

        if (followerUserId == followeeUserId)
            return false;

        var exists = await context.Followers.AnyAsync(f =>
            f.FollowerUserId == followerUserId &&
            f.UserId == followeeUserId);

        if (exists)
            return false;

        var follow = new Followers
        {
            FollowerUserId = followerUserId,
            UserId = followeeUserId,
            CreatedUtc = DateTime.UtcNow
        };

        context.Followers.Add(follow);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnfollowAsync(Guid followerUserId, Guid followeeUserId)
    {
        if (followerUserId == Guid.Empty || followeeUserId == Guid.Empty)
            return false;

        var follow = await context.Followers.FirstOrDefaultAsync(f =>
            f.FollowerUserId == followerUserId &&
            f.UserId == followeeUserId);

        if (follow == null)
            return false;

        context.Followers.Remove(follow);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsFollowingAsync(Guid followerUserId, Guid followeeUserId)
    {
        if (followerUserId == Guid.Empty || followeeUserId == Guid.Empty)
            return false;

        return await context.Followers.AnyAsync(f =>
            f.FollowerUserId == followerUserId &&
            f.UserId == followeeUserId);
    }

    public async Task<int> GetFollowerCountAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return 0;

        return await context.Followers.CountAsync(f =>
            f.UserId == userId);
    }

    public async Task<int> GetFollowingCountAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return 0;

        return await context.Followers.CountAsync(f =>
            f.FollowerUserId == userId);
    }

    public async Task<List<User>> GetFollowersAsync(Guid userId, int limit = 50)
    {
        if (userId == Guid.Empty)
            return new List<User>();

        return await context.Followers
            .Where(f => f.UserId == userId)
            .Include(f => f.Follower)
            .OrderByDescending(f => f.CreatedUtc)
            .Take(limit)
            .Select(f => f.Follower)
            .ToListAsync();
    }

    public async Task<List<User>> GetFollowingAsync(Guid userId, int limit = 50)
    {
        if (userId == Guid.Empty)
            return new List<User>();

        return await context.Followers
            .Where(f => f.FollowerUserId == userId)
            .Include(f => f.Followee)
            .OrderByDescending(f => f.CreatedUtc)
            .Take(limit)
            .Select(f => f.Followee)
            .ToListAsync();
    }
}
