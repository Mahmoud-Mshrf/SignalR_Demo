using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Data;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models;
using SignalR_Demo.Models.Chat_Models;
using SignalR_Demo.Services;

namespace SignalR_Demo.Hubs;
[Authorize]
public sealed class ChatHub(IChatService chatService,GroupService groupService)
    : Hub<IChatClient>
{
    public override Task OnConnectedAsync()
    {
        // var userGroups = chatService.
        return base.OnConnectedAsync();
    }
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

        var newId = Guid.NewGuid();

        Console.WriteLine($"Generated ID: {newId}");

        var message = new Message
        {
            Id = newId,
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        Console.WriteLine($"Message ID after creation: {message.Id}");

        await chatService.AddMessageAsync(message);

        var dto = new SendMessageDto(
            message.ChatId,
            message.SenderId,
            message.Content,
            message.SentAt);

        await Clients.Users(
            senderId.ToString(),
            receiverId.ToString())
            .ReceiveMessage(dto);
    }
    public async Task SendGroupMessage(string content,Guid groupId,Guid userId)
    {
        var message =await groupService.AddGroupMessageAsync(groupId,content,userId);
        await Clients.Group(RoomGroup(groupId.ToString())).ReceiveMessage(message);
    }

    private string RoomGroup(string id) => "group_"+$"{id}";
}
public interface IChatClient
{
    Task ReceiveMessage(SendMessageDto dto);
}
