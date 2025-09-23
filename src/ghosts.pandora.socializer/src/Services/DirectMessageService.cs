using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Services;

public interface IDirectMessageService
{
    Task<DirectMessage> SendMessageAsync(string fromUsername, string toUsername, string message);
    Task<IEnumerable<DirectMessage>> GetConversationAsync(string username1, string username2);
    Task<IEnumerable<DirectMessage>> GetReceivedMessagesAsync(string username);
    Task<IEnumerable<DirectMessage>> GetSentMessagesAsync(string username);
    Task<IEnumerable<DirectMessage>> GetUnreadMessagesAsync(string username);
    Task MarkAsReadAsync(int messageId);
    Task<IEnumerable<string>> GetConversationPartnersAsync(string username);
}

public class DirectMessageService(DataContext context) : IDirectMessageService
{
    public async Task<DirectMessage> SendMessageAsync(string fromUsername, string toUsername, string message)
    {
        var directMessage = new DirectMessage
        {
            FromUsername = fromUsername,
            ToUsername = toUsername,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        context.DirectMessages.Add(directMessage);
        await context.SaveChangesAsync();
        return directMessage;
    }

    public async Task<IEnumerable<DirectMessage>> GetConversationAsync(string username1, string username2)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(dm => (dm.FromUsername == username1 && dm.ToUsername == username2) ||
                         (dm.FromUsername == username2 && dm.ToUsername == username1))
            .OrderBy(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetReceivedMessagesAsync(string username)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Where(dm => dm.ToUsername == username)
            .OrderByDescending(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetSentMessagesAsync(string username)
    {
        return await context.DirectMessages
            .Include(dm => dm.ToUser)
            .Where(dm => dm.FromUsername == username)
            .OrderByDescending(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetUnreadMessagesAsync(string username)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Where(dm => dm.ToUsername == username && dm.ReadUtc == null)
            .OrderByDescending(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int messageId)
    {
        var message = await context.DirectMessages.FindAsync(messageId);
        if (message != null && message.ReadUtc == null)
        {
            message.ReadUtc = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<string>> GetConversationPartnersAsync(string username)
    {
        var partners = await context.DirectMessages
            .Where(dm => dm.FromUsername == username || dm.ToUsername == username)
            .Select(dm => dm.FromUsername == username ? dm.ToUsername : dm.FromUsername)
            .Distinct()
            .ToListAsync();

        return partners;
    }
}