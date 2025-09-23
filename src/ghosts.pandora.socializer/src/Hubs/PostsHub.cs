using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Ghosts.Socializer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Hubs;

public class PostsHub(DataContext dbContext) : Hub
{
    public async Task SendMessage(Guid id, string user, string message, string created)
    {
        if (id == Guid.Empty)
            id = Guid.NewGuid();

        if (string.IsNullOrEmpty(created))
            created = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);

        // Get or create user
        var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == user);
        if (dbUser == null)
        {
            dbUser = new User
            {
                Username = user,
                Bio = $"User {user}",
                Avatar = $"/u/{user}/avatar",
                Status = "online",
            };
            dbContext.Users.Add(dbUser);
            await dbContext.SaveChangesAsync();
        }

        // Get default theme
        var theme = await dbContext.Themes.FirstOrDefaultAsync(t => t.Name == "facebook");
        if (theme == null)
        {
            theme = new Theme
            {
                Name = "facebook",
                DisplayName = "Facebook",
                Description = "Facebook theme"
            };
            dbContext.Themes.Add(theme);
            await dbContext.SaveChangesAsync();
        }

        var post = new Post
        {
            Username = dbUser.Username,
            ThemeId = theme.Id,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();

        await Clients.All.SendAsync("SendMessage", id, user, message, created);
    }
}
