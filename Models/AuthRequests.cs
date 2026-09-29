using System.ComponentModel.DataAnnotations;

namespace SignalR_Demo.Models;

/// <summary>Details required to create a user account.</summary>
public sealed record RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, EmailAddress, StringLength(320)]
    public required string Email { get; init; }

    [Required, StringLength(32, MinimumLength = 1)]
    public required string PhoneNumber { get; init; }

    [Required, StringLength(128, MinimumLength = 8)]
    public required string Password { get; init; }
}

/// <summary>Credentials used to sign in.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(320)]
    public required string Email { get; init; }

    [Required, StringLength(128)]
    public required string Password { get; init; }
}

/// <summary>Access token and basic profile returned after authentication.</summary>
public sealed record AuthResponse(Guid UserId, string Name, string Email, string AccessToken);