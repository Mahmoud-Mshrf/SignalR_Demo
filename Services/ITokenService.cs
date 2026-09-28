using SignalR_Demo.Models;

namespace SignalR_Demo.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken(); // raw, random — caller hashes it before storing
    Guid? GetUserIdFromExpiredToken(string accessToken);
}

