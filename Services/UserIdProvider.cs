using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace SignalR_Demo.Services;

public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext context)
    {
        return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value??
            context.User.FindFirst("Sub")?.Value;
    }
}