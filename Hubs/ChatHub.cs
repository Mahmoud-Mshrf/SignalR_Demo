using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Dtos;
using SignalR_Demo.Models.Chat_Models;
using SignalR_Demo.Services;

namespace SignalR_Demo.Hubs;
[Authorize]
public sealed class ChatHub(
    IChatService chatService,
    IGroupService groupService,
    IHubConnectionTracker connectionTracker)
    : Hub<IChatClient>
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            throw new HubException("Invalid user identity.");

        connectionTracker.Track(userId, Context.ConnectionId);
        try
        {
            var userGroups = await groupService.GetJoinedGroupIdsAsync(userId, Context.ConnectionAborted);
            foreach (var groupId in userGroups)
            {
                var roomName = RoomGroup(groupId);
                await Groups.AddToGroupAsync(Context.ConnectionId, roomName, Context.ConnectionAborted);

                if (!await groupService.IsGroupMemberAsync(userId, groupId, Context.ConnectionAborted))
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            }
        }
        catch
        {
            connectionTracker.Untrack(userId, Context.ConnectionId);
            throw;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            connectionTracker.Untrack(userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
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
            receiverId,
            Context.ConnectionAborted);

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

        await chatService.AddMessageAsync(message, Context.ConnectionAborted);

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
    public async Task SendGroupMessage(string content, Guid groupId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            throw new HubException("Invalid user identity.");

        var message = await groupService.AddGroupMessageAsync(
            groupId,
            content,
            userId,
            Context.ConnectionAborted);
        await Clients.Group(RoomGroup(groupId)).ReceiveMessage(message);
    }
    public async Task JoinRoom(Guid groupId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            throw new HubException("Invalid user identity.");

        if (!await groupService.IsGroupMemberAsync(userId, groupId))
            throw new HubException("Only group members can join the room.");

        var roomName = RoomGroup(groupId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName, Context.ConnectionAborted);

        if (!await groupService.IsGroupMemberAsync(userId, groupId, Context.ConnectionAborted))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName, Context.ConnectionAborted);
            throw new HubException("Only group members can join the room.");
        }
    }

    public static string RoomGroup(Guid groupId) => $"group_{groupId}";
}
public interface IChatClient
{
    Task ReceiveMessage(SendMessageDto dto);
    Task GroupAdded(Guid groupId);
    Task GroupRemoved(Guid groupId);
}
