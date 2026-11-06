using System.Net;
using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IGhostsService
{
    bool IsActive();
    Task CreatePost(Post post);
    Task CreateLike(Like like);
    Task CreateComment(Comment comment);
    Task CreateDirectMessage(DirectMessage directMessage);
}

public class GhostsService(ApplicationConfiguration applicationConfiguration, DataContext context) : IGhostsService
{
    // Shared by every request: the service itself is scoped
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly MemoryCache NpcIds = new(new MemoryCacheOptions());

    // NPCs are created after their users first show up here, so a miss is only remembered briefly
    private static readonly TimeSpan NpcIdLifetime = TimeSpan.FromMinutes(2);

    public bool IsActive()
    {
        return !string.IsNullOrEmpty(applicationConfiguration.Ghosts.ApiUrl);
    }

    public Task CreatePost(Post post)
    {
        Report(post.Username, "SocialMediaPost", post.Message);
        return Task.CompletedTask;
    }

    public async Task CreateComment(Comment comment)
    {
        Report(await Username(comment.UserId), "SocialMediaComment", comment.Message);
    }

    public async Task CreateLike(Like like)
    {
        Report(await Username(like.UserId), "SocialMediaLike", $"Liked {like.PostId.ToString()}");
    }

    public async Task CreateDirectMessage(DirectMessage directMessage)
    {
        Report(await Username(directMessage.FromUserId), "SocialMediaDirectMessage", directMessage.Message);
    }

    private async Task<string> Username(Guid userId)
    {
        return (await context.Users.FindAsync(userId))?.Username;
    }

    /// <summary>
    /// Records the activity for the NPC with this username, if there is one. The request does not wait
    /// for the GHOSTS API, so nothing request-scoped may be used once the task is running.
    /// </summary>
    private void Report(string username, string activityType, string detail)
    {
        if (string.IsNullOrWhiteSpace(username))
            return;

        var apiUrl = applicationConfiguration.Ghosts.ApiUrl;

        _ = Task.Run(async () =>
        {
            try
            {
                var npcId = await NpcId(apiUrl, username);
                if (npcId == null)
                    return;

                var url = $"{apiUrl}/npcs/{npcId}/activity" +
                          $"?activityType={Uri.EscapeDataString(activityType)}" +
                          $"&detail={Uri.EscapeDataString(detail)}";

                using var response = await HttpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not report {activityType} by {username} to GHOSTS: {e.Message}");
            }
        });
    }

    private static async Task<Guid?> NpcId(string apiUrl, string username)
    {
        var key = username.Trim().ToLowerInvariant();
        if (NpcIds.TryGetValue(key, out Guid? npcId))
            return npcId;

        using var response = await HttpClient.GetAsync($"{apiUrl}/npcs/lookup?username={Uri.EscapeDataString(key)}");
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
            npcId = (await response.Content.ReadFromJsonAsync<Npc>())?.Id;
        }

        return NpcIds.Set(key, npcId, NpcIdLifetime);
    }

    private record Npc(Guid Id);
}
