using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models.Chat_Models;
using SignalR_Demo.Services;

namespace SignalR_Demo.Hubs;
[Authorize]
public sealed class ChatHub(IChatService chatService)
    : Hub<IChatClient>
{
    public async Task SendPrivateMessage(
        Guid receiverId,
        string content)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var senderId))
            throw new HubException("Invalid user identity.");

        if (senderId == receiverId)
            throw new HubException(
                "You cannot send a message to yourself.");

        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > 2000)
        {
            throw new HubException(
                "Message must be 1 to 2000 characters.");
        }

        var chatId = await chatService.GetOrCreateChatAsync(
            senderId,
            receiverId);

        var message = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        await chatService.AddMessageAsync(message);

        var dto = new MessageDto(
            message.ChatId,
            message.SenderId,
            message.Content,
            message.SentAt);

        await Clients.Users(
            senderId.ToString(),
            receiverId.ToString())
            .ReceiveMessage(dto);
    }
}
public interface IChatClient
{
    Task ReceiveMessage(MessageDto dto);
}
