using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Socializer.Infrastructure;

namespace Socializer.Hubs;

public class PostsHub: Hub
{
    private readonly ILogger<PostsHub> _logger;
    //private IHubContext<PostsHub> _hubContext;
    private readonly DataContext _db;
    
    public PostsHub(ILogger<PostsHub> logger, DataContext dbContext)
    {
        _logger = logger;
        _db = dbContext;    
    }
    
    public async Task SendMessage(string id, string user, string message, string created)
    {
        if (string.IsNullOrEmpty(id))
            id = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(created))
            created = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
        
        var post = new Post();
        post.Id = id;
        post.User = user;
        post.Message = message;
        post.CreatedUtc = DateTime.UtcNow;
        ;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        
        await Clients.All.SendAsync("SendMessage", id, user, message, created);
    }
}