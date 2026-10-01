using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalR_Demo.Helpers;
using SignalR_Demo.Services;

namespace SignalR_Demo.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ChatsController(IChatService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetChats(
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int size = 10,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var chats = await chatService.GetChatsAsync(userId, page, size, cancellationToken);

        return Ok(chats);
    }

    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid chatId,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int size = 10,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var messages = await chatService.GetMessagesAsync(
            userId,
            chatId,
            page,
            size,
            cancellationToken);

        return Ok(messages);
    }

    [HttpPost("{chatId:guid}/read")]
    public async Task<IActionResult> MarkChatAsRead(
        Guid chatId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();

        var markedAsRead = await chatService.MarkChatAsReadAsync(chatId, userId, cancellationToken);

        return markedAsRead ? NoContent() : NotFound();
    }
}