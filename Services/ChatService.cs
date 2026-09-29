using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Services;

public sealed class ChatService(AppDbContext context)
    : IChatService
{
    public async Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId)
    {
        var firstId = currentUserId.CompareTo(otherUserId) < 0
            ? currentUserId
            : otherUserId;

        var secondId = currentUserId.CompareTo(otherUserId) < 0
            ? otherUserId
            : currentUserId;

        var pairKey = $"{firstId}_{secondId}";

        var existingChat = await context.Chats
            .FirstOrDefaultAsync(x => x.UserPairKey == pairKey);

        if (existingChat is not null)
            return existingChat.Id;

        var chat = Chat.Create(
            currentUserId,
            otherUserId);

        chat.Participants.Add(new ChatParticipant
        {
            ChatId = chat.Id,
            UserId = currentUserId,
            LastReadAt = DateTime.UtcNow
        });

        chat.Participants.Add(new ChatParticipant
        {
            ChatId = chat.Id,
            UserId = otherUserId,
            LastReadAt = DateTime.UtcNow
        });

        context.Chats.Add(chat);

        await context.SaveChangesAsync();

        return chat.Id;
    }

    public async Task AddMessageAsync(Message message)
    {
        context.Messages.Add(message);

        await context.SaveChangesAsync();
    }
}
public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId);

    Task AddMessageAsync(Message message);
}