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

    public async Task<bool> MarkChatAsReadAsync(Guid chatId, Guid currentUserId)
    {
        var participant = await context.ChatParticipants
            .FirstOrDefaultAsync(x =>
                x.ChatId == chatId &&
                x.UserId == currentUserId);

        if (participant is null)
            return false;

        participant.LastReadAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return true;
    }

    public async Task<PaginatedList<ChatDto>> GetChatsAsync(Guid UserId,int page,int size)
    {
        var chats = await context.Chats
            .Where(chat => chat.Participants.Any(participant => participant.UserId == UserId))
            .OrderByDescending(chat => chat.Messages.Max(message => message.SentAt))
            .Skip((page - 1) * size)
            .Take(size)
            .Select(chat => new
            {
                ChatId = chat.Id,
                Receiver = chat.Participants.First(participant => participant.UserId != UserId),
                LastMessage = chat.Messages.OrderByDescending(message => message.SentAt).First(),
                UnreadMessages = chat.Messages.Count(message =>
                    message.SenderId != UserId &&
                    message.SentAt > chat.Participants
                        .First(participant => participant.UserId == UserId)
                        .LastReadAt)
            })
            .Select(chat => new ChatDto(
                chat.ChatId,
                chat.Receiver.UserId,
                chat.Receiver.User.Name,
                chat.LastMessage.Content,
                chat.LastMessage.SentAt,
                chat.LastMessage.SenderId == UserId,
                chat.UnreadMessages))
            .ToListAsync();

        var itemsCount = await context.Chats
            .CountAsync(chat => chat.Participants.Any(participant => participant.UserId == UserId));

        return new PaginatedList<ChatDto>
        {
            Items = chats,
            Page = page,
            PageSize = size,
            TotalCount = itemsCount,
            HasNextPage = (((page - 1) * size) + chats.Count) < itemsCount
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

        var messages = await context.Messages
            .Where(message => message.ChatId == ChatId)
            .OrderBy(message => message.SentAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
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
            HasNextPage= (((page-1)*size) + msgs.Count ) < itemsCount
        };
    }
}
public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId);

    Task AddMessageAsync(Message message);
    Task<bool> MarkChatAsReadAsync(Guid chatId, Guid currentUserId);
    Task<PaginatedList<ChatDto>> GetChatsAsync(Guid UserId,int page,int size);
    Task<PaginatedList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid ChatId,int page,int size);
}