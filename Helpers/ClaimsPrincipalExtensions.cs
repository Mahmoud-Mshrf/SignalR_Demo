using System.Security.Claims;

namespace SignalR_Demo.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("Sub");

        return Guid.TryParse(value, out userId);
    }
}