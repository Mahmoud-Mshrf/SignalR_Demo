using SignalR_Demo.Dtos;
using SignalR_Demo.Helpers;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Services;

public interface IChatService
{
    Task<Guid> GetOrCreateChatAsync(
        Guid currentUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(Message message, CancellationToken cancellationToken = default);

    Task<bool> MarkChatAsReadAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<ChatDto>> GetChatsAsync(
        Guid userId,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<PaginatedList<MessageDto>> GetMessagesAsync(
        Guid userId,
        Guid chatId,
        int page,
        int size,
        CancellationToken cancellationToken = default);
}