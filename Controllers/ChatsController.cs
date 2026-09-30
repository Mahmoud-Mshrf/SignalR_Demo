using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalR_Demo.Services;

namespace SignalR_Demo.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class ChatsController(ChatService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetChats(int page=1, int size = 10)
    {
        var userId =Guid.Parse( HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var chats =await chatService.GetChatsAsync(userId,page,size);

        return Ok(chats);
    }
}