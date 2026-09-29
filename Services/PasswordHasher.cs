using Microsoft.AspNetCore.Identity;
using SignalR_Demo.Models;

namespace SignalR_Demo.Services;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _hasher = new();

    public string Hash(string password) =>
        _hasher.HashPassword(user: null!, password); // user param unused by the algorithm itself

    public bool Verify(string password, string hash)
    {
        var result = _hasher.VerifyHashedPassword(user: null!, hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}