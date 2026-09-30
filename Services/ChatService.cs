using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models;
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
        Console.WriteLine($"Message ID: {message.Id}");
        Console.WriteLine($"Chat ID: {message.ChatId}");
        Console.WriteLine($"Sender ID: {message.SenderId}");

        context.Messages.Add(message);

        await context.SaveChangesAsync();
    }

    public async Task<List<ChatDto>> GetChatsAsync(Guid UserId,int page,int size)
    {
        var chats = await context.Chats
            .Include(chat => chat.Participants)
                .ThenInclude(participant => participant.User)
            .Include(chat => chat.Messages)
            .Where(chat => chat.Participants.Any(participant => participant.UserId == UserId))
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
        List<ChatDto> list= new();
        foreach (var chat in chats)
        {
            var receiver = chat.Participants.First(x=>x.UserId!=UserId);
            var lastMessage = chat.Messages.OrderBy(x=>x.SentAt).First();
            bool sentByMe = lastMessage.SenderId==UserId;
            list.Add(new ChatDto(chat.Id,receiver.UserId,receiver.User.Name,lastMessage.Content,lastMessage.SentAt,sentByMe));
        }
        return  list;
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid ChatId,int page,int size)
    {
        var chat =await context.Chats.Include(x=>x.Participants).FirstOrDefaultAsync(c=>c.Id==ChatId);
        if (chat is null)
        {
            throw new ArgumentNullException();
        }
        if (!chat.Participants.Any(x=>x.UserId==userId))
        {
            throw new UnauthorizedAccessException();
        }

        var messages =await context.Messages.Where(x=>x.ChatId==ChatId).Skip((page - 1)* size).Take(size).OrderBy(x=>x.SentAt).ToListAsync();
        var msgs= new List<ChatMessageDto>();
        foreach (var msg in messages)
        {
            msgs.Add(new ChatMessageDto(msg.SenderId,msg.Content,msg.SentAt));
        }
        return msgs;
    }
}
public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId);

    Task AddMessageAsync(Message message);
    Task<List<ChatDto>> GetChatsAsync(Guid UserId,int page,int size);
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid ChatId,int page,int size);
}