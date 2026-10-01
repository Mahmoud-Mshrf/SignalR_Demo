using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalR_Demo.Services;

namespace SignalR_Demo.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ChatsController(IChatService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetChats(int page=1, int size = 10)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var chats =await chatService.GetChatsAsync(userId,page,size);

        return Ok(chats);
    }

    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatId, int page = 1, int size = 10)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var messages = await chatService.GetMessagesAsync(userId, chatId, page, size);
            return Ok(messages);
        }
        catch (ArgumentNullException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{chatId:guid}/read")]
    public async Task<IActionResult> MarkChatAsRead(Guid chatId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var markedAsRead = await chatService.MarkChatAsReadAsync(chatId, userId);

        return markedAsRead ? NoContent() : NotFound();
    }
}