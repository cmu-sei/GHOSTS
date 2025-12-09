using Ghosts.Pandora.Infrastructure.Models;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IGhostsService
{
    bool IsActive();
    Task CreatePost(Post post);
    Task CreateLike(Like like);
    Task CreateComment(Comment comment);
    Task CreateDirectMessage(DirectMessage directMessage);
    Task CreateUser(User user);
}

public class GhostsService(ApplicationConfiguration applicationConfiguration) : IGhostsService
{
    private readonly HttpClient _httpClient = new();

    public bool IsActive()
    {
        return !string.IsNullOrEmpty(applicationConfiguration.Ghosts.ApiUrl);
    }

    public async Task CreateUser(User user)
    {
        var url = $"{applicationConfiguration.Ghosts.ApiUrl}/npcs/list" +
                  $"?id={Uri.EscapeDataString(user.Id.ToString())}" +
                  $"&username={Uri.EscapeDataString(user.Username)}" +
                  $"&originUrl={Uri.EscapeDataString(user.Theme)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreatePost(Post post)
    {
        var activityType = "SocialMediaPost";

        var url = $"{applicationConfiguration.Ghosts.ApiUrl}/npcs/{post.UserId}/activity" +
                  $"?activityType={Uri.EscapeDataString(activityType)}" +
                  $"&detail={Uri.EscapeDataString(post.Message)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreateComment(Comment comment)
    {
        var activityType = "SocialMediaComment";

        var url = $"{applicationConfiguration.Ghosts.ApiUrl}/npcs/{comment.UserId}/activity" +
                  $"?activityType={Uri.EscapeDataString(activityType)}" +
                  $"&detail={Uri.EscapeDataString(comment.Message)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreateLike(Like like)
    {
        var activityType = "SocialMediaLike";

        var url = $"{applicationConfiguration.Ghosts.ApiUrl}/npcs/{like.UserId}/activity" +
                  $"?activityType={Uri.EscapeDataString(activityType)}" +
                  $"&detail={Uri.EscapeDataString($"Liked {like.PostId.ToString()}")}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task CreateDirectMessage(DirectMessage directMessage)
    {
        var activityType = "SocialMediaDirectMessage";

        var url = $"{applicationConfiguration.Ghosts.ApiUrl}/npcs/{directMessage.FromUserId}/activity" +
                  $"?activityType={Uri.EscapeDataString(activityType)}" +
                  $"&detail={Uri.EscapeDataString(directMessage.Message)}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
