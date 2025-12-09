using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IPostService
{
    bool Duplicates(Post post);

    // Theme-based queries
    Task<List<Post>> GetPostsByTheme(string themeName, int limit = 50, int offset = 0);
    Task<List<Post>> GetLatestPostsByTheme(string themeName, int count = 100);

    // User-based queries
    Task<List<Post>> GetPostsByUserId(Guid id, int limit = 50, int offset = 0);

    // Combined user + theme queries
    Task<List<Post>> GetPostsByUserAndTheme(string username, string themeName, int limit = 50, int offset = 0);

    // General queries
    Task<List<Post>> GetAllPosts(int limit = 50, int offset = 0);
    Task<Post> GetPostById(Guid postId);
    Task<Post> CreatePost(Guid userId, string username, string themeName, string message);
    Task<Post> CreatePost(Post post);
    Task<bool> DeletePost(Guid postId, Guid userId);

    // Statistics
    Task<int> GetPostCountByTheme(string themeName);
    Task<int> GetPostCountByUser(string username);
    Task<Dictionary<string, int>> GetPostCountsByTheme();
    Task<IEnumerable<Like>> GetLikes(Guid postId);

    // Search
    Task<List<Post>> SearchPosts(string query, string themeName = null, int limit = 50, int offset = 0);

    Task LikePost(Guid postId, Guid userId);
    Task<IEnumerable<Comment>> GetComments(Guid postId);
    Task<Comment> CreateComment(Guid postId, Guid userId, string message);
    Task RemovePosts();
}

public class PostService(DataContext context, ApplicationConfiguration applicationConfiguration, IGhostsService ghostsService)
    : IPostService
{
    public bool Duplicates(Post post)
    {
        // has the same user tried to post the same message within the past x minutes?
        return context.Posts.Any(p => p.Message.ToLower() == post.Message.ToLower()
           && p.UserId == post.UserId
           && p.CreatedUtc > post.CreatedUtc.AddMinutes(-applicationConfiguration.MinutesToCheckForDuplicatePost));
    }

    public async Task RemovePosts()
    {
        context.Posts.RemoveRange(context.Posts);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Like>> GetLikes(Guid postId)
    {
        return await context.Likes
            .Include(l => l.User)
            .Where(x => x.PostId == postId)
            .ToListAsync();
    }

    public async Task LikePost(Guid postId, Guid userId)
    {
        var like = new Like
        {
            PostId = postId,
            UserId = userId,
            CreatedUtc = DateTime.UtcNow
        };

        context.Likes.Add(like);
        await context.SaveChangesAsync();

        if(ghostsService.IsActive())
            await ghostsService.CreateLike(like);
    }

    public async Task<IEnumerable<Comment>> GetComments(Guid postId)
    {
        return await context.Comments
            .Include(c => c.User)
            .Where(x => x.PostId == postId)
            .ToListAsync<Comment>();
    }

    public async Task<Comment> CreateComment(Guid postId, Guid userId, string message)
    {
        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            Message = message,
            CreatedUtc = DateTime.UtcNow,
        };

        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        if(ghostsService.IsActive())
            await ghostsService.CreateComment(comment);

        return comment;
    }

    // Theme-based queries
    public async Task<List<Post>> GetPostsByTheme(string themeName, int limit = 50, int offset = 0)
    {
        return await context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .Where(p => p.Theme.ToLower() == themeName.ToLower())
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    // public async Task<IReadOnlyList<Post>> GetPersonalizedFeedAsync(
    //     string viewerUsername,
    //     string themeName,
    //     int limit = 50,
    //     int offset = 0)
    // {
    //     var followed = await _context.Followers
    //         .Where(f => f.FollowerUsername == viewerUsername)
    //         .Select(f => f.Username)
    //         .ToListAsync();
    //
    //     var seed = await _context.Posts
    //         .Include(p => p.User)
    //         .Include(p => p.Likes)
    //         .Include(p => p.Comments)
    //         .Where(p => p.Theme == themeName &&
    //                     (followed.Contains(p.Username) || p.Username == viewerUsername))
    //         .AsNoTracking()
    //         .ToListAsync();
    //
    //     var now = DateTime.UtcNow;
    //
    //     var ranked = seed
    //         .Select(p => new
    //         {
    //             Post = p,
    //             Score =
    //                 RecencyScore(p, now) * 0.5 +
    //                 AffinityScore(p, viewerUsername) * 0.3 +
    //                 EngagementScore(p) * 0.2
    //         })
    //         .OrderByDescending(x => x.Score)
    //         .ThenByDescending(x => x.Post.CreatedUtc)
    //         .Skip(offset)
    //         .Take(limit)
    //         .Select(x => x.Post)
    //         .ToList();
    //
    //     return ranked;
    // }

    public async Task<List<Post>> GetLatestPostsByTheme(string themeName, int count = 100)
    {
        return await GetPostsByTheme(themeName, count, 0);
    }

    // User-based queries
    public async Task<List<Post>> GetPostsByUserId(Guid id, int limit = 50, int offset = 0)
    {
        return await context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .Where(p => p.User.Id == id)
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    // Combined user + theme queries
    public async Task<List<Post>> GetPostsByUserAndTheme(string username, string themeName, int limit = 50,
        int offset = 0)
    {
        return await context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .Where(p => p.User.Username.ToLower() == username.ToLower() &&
                        p.Theme.ToLower() == themeName.ToLower())
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }


    // General queries
    public async Task<List<Post>> GetAllPosts(int limit = 50, int offset = 0)
    {
        return await context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Post>> SearchPosts(string query, string themeName = null, int limit = 50,
        int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<Post>();
        }

        var normalized = query.Trim().ToLowerInvariant();

        var posts = context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .Where(p => p.Message.ToLower().Contains(normalized) ||
                        p.Username.ToLower().Contains(normalized) ||
                        p.Comments.Any(c => c.Message.ToLower().Contains(normalized)));

        if (!string.IsNullOrWhiteSpace(themeName))
        {
            var themeNormalized = themeName.ToLowerInvariant();
            posts = posts.Where(p => p.Theme.ToLower() == themeNormalized);
        }

        return await posts
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Post> GetPostById(Guid postId)
    {
        return await context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(p => p.Id == postId);
    }

    public async Task<Post> CreatePost(Guid userId, string username, string themeName, string message)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Username = username,
            UserId = userId,
            Theme = themeName,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        return await CreatePost(post);
    }

    public async Task<Post> CreatePost(Post post)
    {
        if (Duplicates(post))
            return post;

        context.Posts.Add(post);
        await context.SaveChangesAsync();
        post = await GetPostById(post.Id);

        if(ghostsService.IsActive())
            await ghostsService.CreatePost(post);

        return post;
    }

    public async Task<bool> DeletePost(Guid postId, Guid userId)
    {
        var post = await context.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId);

        if (post == null)
            return false;

        context.Posts.Remove(post);
        await context.SaveChangesAsync();
        return true;
    }

    // Statistics
    public async Task<int> GetPostCountByTheme(string themeName)
    {
        return await context.Posts
            .Where(p => p.Theme.ToLower() == themeName.ToLower())
            .CountAsync();
    }

    public async Task<int> GetPostCountByUser(string username)
    {
        return await context.Posts
            .Where(p => p.User.Username.ToLower() == username.ToLower())
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByTheme()
    {
        return await context.Posts
            .GroupBy(p => p.Theme)
            .Select(g => new { Theme = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Theme, x => x.Count);
    }
}
