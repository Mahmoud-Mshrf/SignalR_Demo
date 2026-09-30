using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Helpers;
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

    public async Task<PaginatedList<ChatDto>> GetChatsAsync(Guid UserId,int page,int size)
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
        var itemsCount = context.Chats.Where(c=>c.Participants.Any(x=>x.UserId==UserId)).Count();
        return new PaginatedList<ChatDto>
        {
            Items=list,
            Page=page,
            PageSize=size,
            TotalCount=itemsCount,
            HasNextPage= (((page-1)*size) + list.Count ) > itemsCount
        };
    }

    public async Task<PaginatedList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid ChatId,int page,int size)
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
        var itemsCount = context.Messages.Where(x=>x.ChatId==ChatId).Count();
        return new PaginatedList<ChatMessageDto>
        {
            Items=msgs,
            Page=page,
            PageSize=size,
            TotalCount=itemsCount,
            HasNextPage= (((page-1)*size) + msgs.Count ) > itemsCount
        };
    }
}
public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId);

    Task AddMessageAsync(Message message);
    Task<PaginatedList<ChatDto>> GetChatsAsync(Guid UserId,int page,int size);
    Task<PaginatedList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid ChatId,int page,int size);
}