using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Hubs;

public class PostsHub(ILogger<PostsHub> logger, DataContext dbContext) : Hub
{
    private readonly ILogger<PostsHub> _logger = logger;
    //private IHubContext<PostsHub> _hubContext;
    private readonly DataContext _db = dbContext;

    public async Task SendMessage(string id, string user, string message, string created)
    {
        if (string.IsNullOrEmpty(id))
            id = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(created))
            created = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);

        // Get or create user
        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == user);
        if (dbUser == null)
        {
            dbUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = user,
                DisplayName = user,
                Bio = $"User {user}",
                Avatar = $"/u/{user}/avatar",
                Status = "online",
                CreatedUtc = DateTime.UtcNow,
                LastActiveUtc = DateTime.UtcNow
            };
            _db.Users.Add(dbUser);
            await _db.SaveChangesAsync();
        }

        // Get default theme
        var theme = await _db.Themes.FirstOrDefaultAsync(t => t.Name == "facebook");
        if (theme == null)
        {
            theme = new Theme
            {
                Name = "facebook",
                DisplayName = "Facebook",
                Description = "Facebook theme"
            };
            _db.Themes.Add(theme);
            await _db.SaveChangesAsync();
        }

        var post = new Post
        {
            Id = id,
            UserId = dbUser.Id,
            ThemeId = theme.Id,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        await Clients.All.SendAsync("SendMessage", id, user, message, created);
    }
}
