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
    private const int MaximumPageSize = 100;

    public async Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        var firstId = currentUserId.CompareTo(otherUserId) < 0
            ? currentUserId
            : otherUserId;

        var secondId = currentUserId.CompareTo(otherUserId) < 0
            ? otherUserId
            : currentUserId;

        var pairKey = $"{firstId}_{secondId}";

        var existingChat = await context.Chats
            .FirstOrDefaultAsync(chat => chat.UserPairKey == pairKey, cancellationToken);

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

        await context.SaveChangesAsync(cancellationToken);

        return chat.Id;
    }

    public async Task AddMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> MarkChatAsReadAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var participant = await context.ChatParticipants
            .FirstOrDefaultAsync(participant =>
                participant.ChatId == chatId && participant.UserId == currentUserId,
                cancellationToken);

        if (participant is null)
            return false;

        participant.LastReadAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PaginatedList<ChatDto>> GetChatsAsync(
        Guid userId,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, size);

        var chats = await context.Chats
            .Where(chat => chat.Participants.Any(participant => participant.UserId == userId))
            .Where(chat => chat.Messages.Any())
            .OrderByDescending(chat => chat.Messages.Max(message => message.SentAt))
            .Skip((page - 1) * size)
            .Take(size)
            .Select(chat => new
            {
                ChatId = chat.Id,
                Receiver = chat.Participants
                    .Where(participant => participant.UserId != userId)
                    .Select(participant => new
                    {
                        participant.UserId,
                        participant.User.Name
                    })
                    .First(),
                LastMessage = chat.Messages
                    .OrderByDescending(message => message.SentAt)
                    .Select(message => new
                    {
                        message.Content,
                        message.SentAt,
                        message.SenderId
                    })
                    .First(),
                UnreadMessages = chat.Messages.Count(message =>
                    message.SenderId != userId &&
                    message.SentAt > chat.Participants
                        .First(participant => participant.UserId == userId)
                        .LastReadAt)
            })
            .Select(chat => new ChatDto(
                chat.ChatId,
                chat.Receiver.UserId,
                chat.Receiver.Name,
                chat.LastMessage.Content,
                chat.LastMessage.SentAt,
                chat.LastMessage.SenderId == userId,
                chat.UnreadMessages))
            .ToListAsync(cancellationToken);

        var itemsCount = await context.Chats
            .CountAsync(chat =>
                chat.Participants.Any(participant => participant.UserId == userId) &&
                chat.Messages.Any(),
                cancellationToken);

        return new PaginatedList<ChatDto>
        {
            Items = chats,
            Page = page,
            PageSize = size,
            TotalCount = itemsCount,
            HasNextPage = (((page - 1) * size) + chats.Count) < itemsCount
        };
    }

    public async Task<PaginatedList<MessageDto>> GetMessagesAsync(
        Guid userId,
        Guid chatId,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, size);

        if (!await context.Chats.AnyAsync(chat => chat.Id == chatId, cancellationToken))
            throw new KeyNotFoundException("Chat was not found.");

        var isParticipant = await context.ChatParticipants.AnyAsync(participant =>
            participant.ChatId == chatId && participant.UserId == userId,
            cancellationToken);

        if (!isParticipant)
            throw new UnauthorizedAccessException("Only chat participants can view messages.");

        var messageQuery = context.Messages.Where(message => message.ChatId == chatId);
        var totalCount = await messageQuery.CountAsync(cancellationToken);
        var messages = await context.Messages
            .Where(message => message.ChatId == chatId)
            .OrderBy(message => message.SentAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(message => new MessageDto(message.SenderId, message.Content, message.SentAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<MessageDto>
        {
            Items = messages,
            Page = page,
            PageSize = size,
            TotalCount = totalCount,
            HasNextPage = ((page - 1) * size) + messages.Count < totalCount
        };
    }

    private static void ValidatePagination(int page, int size)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");

        if (size is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(size), $"Page size must be between 1 and {MaximumPageSize}.");
    }
}