using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IDirectMessageService
{
    Task<DirectMessage> SendMessageAsync(Guid fromUserId, Guid toUserId, string message);
    Task<IEnumerable<DirectMessage>> GetConversationAsync(Guid userId1, Guid userId2);
    Task<IEnumerable<DirectMessage>> GetReceivedMessagesAsync(Guid userId);
    Task<IEnumerable<DirectMessage>> GetSentMessagesAsync(Guid userId);
    Task<IEnumerable<DirectMessage>> GetUnreadMessagesAsync(Guid userId);
    Task MarkAsReadAsync(int messageId);
    Task<IEnumerable<User>> GetConversationPartnersAsync(Guid userId);
}

public class DirectMessageService(DataContext context) : IDirectMessageService
{
    public async Task<DirectMessage> SendMessageAsync(Guid fromUserId, Guid toUserId, string message)
    {
        var directMessage = new DirectMessage
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        };

        context.DirectMessages.Add(directMessage);
        await context.SaveChangesAsync();
        return directMessage;
    }

    public async Task<IEnumerable<DirectMessage>> GetConversationAsync(Guid userId1, Guid userId2)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(dm => (dm.FromUserId == userId1 && dm.ToUserId == userId2) ||
                         (dm.FromUserId == userId2 && dm.ToUserId == userId1))
            .OrderBy(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetReceivedMessagesAsync(Guid userId)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(dm => dm.ToUserId == userId)
            .OrderByDescending(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetSentMessagesAsync(Guid userId)
    {
        return await context.DirectMessages
            .Include(dm => dm.ToUser)
            .Include(dm => dm.FromUser)
            .Where(dm => dm.FromUserId == userId)
            .OrderByDescending(dm => dm.CreatedUtc)
            .ToListAsync();
    }

    public async Task<IEnumerable<DirectMessage>> GetUnreadMessagesAsync(Guid userId)
    {
        return await context.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(dm => dm.ToUserId == userId && dm.ReadUtc == null)
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

    public async Task<IEnumerable<User>> GetConversationPartnersAsync(Guid userId)
    {
        var partnerIds = await context.DirectMessages
            .Where(dm => dm.FromUserId == userId || dm.ToUserId == userId)
            .Select(dm => dm.FromUserId == userId ? dm.ToUserId : dm.FromUserId)
            .Distinct()
            .ToListAsync();

        if (partnerIds.Count == 0)
        {
            return Enumerable.Empty<User>();
        }

        return await context.Users
            .Where(u => partnerIds.Contains(u.Id))
            .ToListAsync();
    }
}
