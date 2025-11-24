using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Hubs;

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

        var post = new Post
        {
            Username = dbUser.Username,
            Theme = dbUser.Theme,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();

        await Clients.All.SendAsync("SendMessage", id, user, message, created);
    }
}
