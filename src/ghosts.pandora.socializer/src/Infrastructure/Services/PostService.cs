using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IPostService
{
    // Theme-based queries
    Task<List<Post>> GetPostsByThemeAsync(string themeName, int limit = 50, int offset = 0);
    Task<List<Post>> GetLatestPostsByThemeAsync(string themeName, int count = 100);

    // User-based queries
    Task<List<Post>> GetPostsByUserAsync(string username, int limit = 50, int offset = 0);

    // Combined user + theme queries
    Task<List<Post>> GetPostsByUserAndThemeAsync(string username, string themeName, int limit = 50, int offset = 0);

    // General queries
    Task<List<Post>> GetAllPostsAsync(int limit = 50, int offset = 0);
    Task<Post> GetPostByIdAsync(Guid postId);
    Task<Post> CreatePostAsync(string username, string themeName, string message);
    Task<bool> DeletePostAsync(Guid postId, string username);

    // Statistics
    Task<int> GetPostCountByThemeAsync(string themeName);
    Task<int> GetPostCountByUserAsync(string username);
    Task<Dictionary<string, int>> GetPostCountsByThemeAsync();
}

public class PostService : IPostService
{
    private readonly DataContext _context;

    public PostService(DataContext context)
    {
        _context = context;
    }

    // Theme-based queries
    public async Task<List<Post>> GetPostsByThemeAsync(string themeName, int limit = 50, int offset = 0)
    {
        return await _context.Posts
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


    public async Task<List<Post>> GetLatestPostsByThemeAsync(string themeName, int count = 100)
    {
        return await GetPostsByThemeAsync(themeName, count, 0);
    }

    // User-based queries
    public async Task<List<Post>> GetPostsByUserAsync(string username, int limit = 50, int offset = 0)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .Where(p => p.User.Username.ToLower() == username.ToLower())
            .OrderByDescending(p => p.CreatedUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }


    // Combined user + theme queries
    public async Task<List<Post>> GetPostsByUserAndThemeAsync(string username, string themeName, int limit = 50, int offset = 0)
    {
        return await _context.Posts
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
    public async Task<List<Post>> GetAllPostsAsync(int limit = 50, int offset = 0)
    {
        return await _context.Posts
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

    public async Task<Post> GetPostByIdAsync(Guid postId)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Likes)
            .ThenInclude(l => l.User)
            .Include(p => p.Comments)
            .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(p => p.Id == postId);
    }

    public async Task<Post> CreatePostAsync(string username, string themeName, string message)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Username = username,
            Theme = themeName,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        return await GetPostByIdAsync(post.Id);
    }

    public async Task<bool> DeletePostAsync(Guid postId, string username)
    {
        var post = await _context.Posts
            .FirstOrDefaultAsync(p => p.Id == postId && p.Username == username);

        if (post == null)
            return false;

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
        return true;
    }

    // Statistics
    public async Task<int> GetPostCountByThemeAsync(string themeName)
    {
        return await _context.Posts
            .Where(p => p.Theme.ToLower() == themeName.ToLower())
            .CountAsync();
    }

    public async Task<int> GetPostCountByUserAsync(string username)
    {
        return await _context.Posts
            .Where(p => p.User.Username.ToLower() == username.ToLower())
            .CountAsync();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByThemeAsync()
    {
        return await _context.Posts
            .GroupBy(p => p.Theme)
            .Select(g => new { Theme = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Theme, x => x.Count);
    }
}
