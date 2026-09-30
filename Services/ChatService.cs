using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
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

    public async Task<List<ChatDto>> GetChatsAsync(Guid UserId,int page=1,int size=10)
    {
        var chats =await context.Chats.Include(x=>x.Participants).Where(x=>x.Participants.Any(x=>x.UserId==UserId)).Skip((page-1)*size).Take(size).ToListAsync();
        List<ChatDto> list= new();
        foreach (var chat in chats)
        {
            var receiver = chat.Participants.First(x=>x.UserId!=UserId);
            var lastMessage = chat.Messages.OrderBy(x=>x.SentAt).First();
            list.Add(new ChatDto(chat.Id,receiver.UserId,receiver.User.Name,lastMessage.Content,lastMessage.SentAt));
        }
        return  list;
    }
}
public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId);

    Task AddMessageAsync(Message message);
    Task<List<ChatDto>> GetChatsAsync(Guid UserId,int page=1,int size=10);
}