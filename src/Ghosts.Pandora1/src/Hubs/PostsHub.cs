using Microsoft.AspNetCore.SignalR;
using Ghosts.Pandora.Infrastructure.Services;

namespace Ghosts.Pandora.Hubs;

public class PostsHub(IPostService postService, IUserService userService) : Hub
{
    public async Task SendMessage(Guid id, string username, string theme, string message, string created)
    {
        // Get or create user
        var user = await userService.GetOrCreateUserAsync(username, theme);
        var post = await postService.CreatePost(user.Id, user.Username, user.Theme, message);
        await Clients.All.SendAsync("SendMessage", post.Id, user.Username, user.Theme, message, post.CreatedUtc);
    }
}
