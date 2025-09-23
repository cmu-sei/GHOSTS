using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

/// <summary>
/// Example queries demonstrating the specific use cases mentioned:
/// - "give me all the facebook posts for a user"
/// - "give me the last 100 posts on reddit"
/// </summary>
public class QueryExamples
{
    private readonly IPostService _postService;
    private readonly IUserService _userService;
    private readonly IThemeService _themeService;

    public QueryExamples(IPostService postService, IUserService userService, IThemeService themeService)
    {
        _postService = postService;
        _userService = userService;
        _themeService = themeService;
    }

    /// <summary>
    /// Get all Facebook posts for a specific user
    /// Example: "give me all the facebook posts for alice"
    /// </summary>
    public async Task<List<Post>> GetAllFacebookPostsForUserAsync(string username)
    {
        return await _postService.GetPostsByUserAndThemeAsync(username, "facebook", limit: 1000);
    }

    /// <summary>
    /// Get the last 100 posts on Reddit (most recent first)
    /// Example: "give me the last 100 posts on reddit"
    /// </summary>
    public async Task<List<Post>> GetLast100RedditPostsAsync()
    {
        return await _postService.GetLatestPostsByThemeAsync("reddit", 100);
    }

    /// <summary>
    /// Get all Instagram posts for a user
    /// </summary>
    public async Task<List<Post>> GetAllInstagramPostsForUserAsync(string username)
    {
        return await _postService.GetPostsByUserAndThemeAsync(username, "instagram", limit: 1000);
    }

    /// <summary>
    /// Get the latest 50 posts on LinkedIn
    /// </summary>
    public async Task<List<Post>> GetLatestLinkedInPostsAsync()
    {
        return await _postService.GetPostsByThemeAsync("linkedin", limit: 50);
    }

    /// <summary>
    /// Get all X/Twitter posts for a user
    /// </summary>
    public async Task<List<Post>> GetAllTwitterPostsForUserAsync(string username)
    {
        return await _postService.GetPostsByUserAndThemeAsync(username, "x", limit: 1000);
    }

    /// <summary>
    /// Get paginated Discord messages (like chat history)
    /// </summary>
    public async Task<List<Post>> GetDiscordChatHistoryAsync(int page = 0, int pageSize = 50)
    {
        return await _postService.GetPostsByThemeAsync("discord", limit: pageSize, offset: page * pageSize);
    }

    /// <summary>
    /// Get all YouTube videos/posts for a user
    /// </summary>
    public async Task<List<Post>> GetAllYouTubePostsForUserAsync(string username)
    {
        return await _postService.GetPostsByUserAndThemeAsync(username, "youtube", limit: 1000);
    }

    /// <summary>
    /// Get user's activity across all themes
    /// </summary>
    public async Task<Dictionary<string, List<Post>>> GetUserActivityAcrossAllThemesAsync(string username)
    {
        var allThemes = await _themeService.GetActiveThemesAsync();
        var result = new Dictionary<string, List<Post>>();

        foreach (var theme in allThemes)
        {
            var posts = await _postService.GetPostsByUserAndThemeAsync(username, theme.Name, limit: 100);
            if (posts.Any())
            {
                result[theme.DisplayName] = posts;
            }
        }

        return result;
    }

    /// <summary>
    /// Get recent activity summary for all themes
    /// </summary>
    public async Task<Dictionary<string, List<Post>>> GetRecentActivityByThemeAsync(int hoursBack = 24)
    {
        var themes = await _themeService.GetActiveThemesAsync();
        var result = new Dictionary<string, List<Post>>();
        var cutoffTime = DateTime.UtcNow.AddHours(-hoursBack);

        foreach (var theme in themes)
        {
            var posts = await _postService.GetPostsByThemeAsync(theme.Name, limit: 100);
            var recentPosts = posts.Where(p => p.CreatedUtc > cutoffTime).ToList();

            if (recentPosts.Any())
            {
                result[theme.DisplayName] = recentPosts;
            }
        }

        return result;
    }

    /// <summary>
    /// Get post statistics by theme
    /// </summary>
    public async Task<Dictionary<string, object>> GetPostStatisticsByThemeAsync()
    {
        var themes = await _themeService.GetActiveThemesAsync();
        var stats = new Dictionary<string, object>();

        foreach (var theme in themes)
        {
            var totalPosts = await _postService.GetPostCountByThemeAsync(theme.Name);
            var recentPosts = await _postService.GetPostsByThemeAsync(theme.Name, limit: 10);

            stats[theme.DisplayName] = new
            {
                TotalPosts = totalPosts,
                RecentPostsCount = recentPosts.Count,
                MostRecentPost = recentPosts.FirstOrDefault()?.CreatedUtc,
                Theme = theme
            };
        }

        return stats;
    }

    /// <summary>
    /// Search posts across themes by content
    /// </summary>
    public async Task<Dictionary<string, List<Post>>> SearchPostsAcrossThemesAsync(string searchTerm)
    {
        var themes = await _themeService.GetActiveThemesAsync();
        var results = new Dictionary<string, List<Post>>();

        foreach (var theme in themes)
        {
            var posts = await _postService.GetPostsByThemeAsync(theme.Name, limit: 1000);
            var matchingPosts = posts
                .Where(p => p.Message.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();

            if (matchingPosts.Any())
            {
                results[theme.DisplayName] = matchingPosts;
            }
        }

        return results;
    }
}
