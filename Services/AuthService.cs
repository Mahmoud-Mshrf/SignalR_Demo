using Microsoft.EntityFrameworkCore;
using SignalR_Demo.Data;
using SignalR_Demo.Models;

namespace SignalR_Demo.Services;

public sealed class AuthService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Employee
        };

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await dbContext.Users.AsNoTracking()
                    .AnyAsync(existingUser => existingUser.Email == email, cancellationToken))
                return null;

            throw;
        }

        return CreateResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            existingUser => existingUser.Email == email,
            cancellationToken);

        if (user is null || user.Disabled || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        tokenService.GenerateAccessToken(user));

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}