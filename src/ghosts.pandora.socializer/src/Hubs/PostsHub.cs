using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Socializer.Infrastructure;

namespace Socializer.Hubs;

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

        var post = new Post
        {
            Id = id,
            User = user,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };
        ;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        await Clients.All.SendAsync("SendMessage", id, user, message, created);
    }
}
