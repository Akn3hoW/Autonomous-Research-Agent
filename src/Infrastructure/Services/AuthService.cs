using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutonomousResearchAgent.Application.Auth;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Users;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    ITokenService tokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.IsActive, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Login failed: user {Email} not found", command.Email);
            throw new AuthenticationException("Invalid email or password.");
        }

        if (!VerifyPassword(command.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed: invalid password for user {UserId} ({Email})", user.Id, command.Email);
            throw new AuthenticationException("Invalid email or password.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(user.Id, user.Email, roles);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken();

        await SaveRefreshTokenAsync(user.Id, refreshToken, refreshExpiresAt, cancellationToken);

        logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new AuthResult(
            accessToken,
            refreshToken,
            accessExpiresAt,
            new UserDto(user.Id, user.Email, user.Username, roles));
    }

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await dbContext.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            logger.LogWarning("Registration failed: email {Email} already exists", command.Email);
            throw new ConflictException($"User with email '{command.Email}' already exists.");
        }

        if (await dbContext.Users.AnyAsync(u => u.Username == command.Username, cancellationToken))
        {
            logger.LogWarning("Registration failed: username {Username} already exists", command.Username);
            throw new ConflictException($"User with username '{command.Username}' already exists.");
        }

        var user = new User
        {
            Email = command.Email,
            Username = command.Username,
            PasswordHash = HashPassword(command.Password)
        };

        var defaultRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.ReadOnly, cancellationToken)
            ?? new Role { Name = RoleNames.ReadOnly };

        if (defaultRole.Id == Guid.Empty)
        {
            dbContext.Roles.Add(defaultRole);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = new List<string> { defaultRole.Name };
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(user.Id, user.Email, roles);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken();

        await SaveRefreshTokenAsync(user.Id, refreshToken, refreshExpiresAt, cancellationToken);

        logger.LogInformation("User {UserId} registered successfully", user.Id);

        return new AuthResult(
            accessToken,
            refreshToken,
            accessExpiresAt,
            new UserDto(user.Id, user.Email, user.Username, roles));
    }

    public async Task<AuthResult> RefreshTokenAsync(TokenRefreshCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var principal = tokenService.ValidateToken(command.RefreshToken);
        if (principal == null)
        {
            throw new AuthenticationException("Invalid or expired refresh token.");
        }

        var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub) ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new AuthenticationException("Invalid refresh token claims.");
        }

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
        {
            throw new AuthenticationException("User not found or inactive.");
        }

        var storedTokenHash = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);

        if (storedTokenHash == null || !VerifyRefreshToken(command.RefreshToken, storedTokenHash.TokenHash))
        {
            throw new AuthenticationException("Refresh token has been revoked or expired.");
        }

        var familyId = storedTokenHash.FamilyId ?? storedTokenHash.Id;

        dbContext.RefreshTokens.Remove(storedTokenHash);
        await dbContext.SaveChangesAsync(cancellationToken);

        var userRoles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var (accessToken, accessExpiresAt) = tokenService.GenerateAccessToken(user.Id, user.Email, userRoles);
        var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken();

        await SaveRefreshTokenAsync(user.Id, refreshToken, refreshExpiresAt, cancellationToken, familyId);

        logger.LogInformation("Token refreshed for user {UserId}", user.Id);

        return new AuthResult(
            accessToken,
            refreshToken,
            accessExpiresAt,
            new UserDto(user.Id, user.Email, user.Username, userRoles));
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Revoked all refresh tokens for user {UserId}", userId);
    }

    public async Task<bool> DetectCompromisedTokenAsync(string token, string currentIpAddress, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(token);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null)
            return false;

        if (storedToken.IpAddress != null && storedToken.IpAddress != currentIpAddress)
        {
            logger.LogWarning("Potential compromised token detected for user {UserId}. Original IP: {OriginalIp}, Current IP: {CurrentIp}",
                storedToken.UserId, storedToken.IpAddress, currentIpAddress);
            return true;
        }

        return false;
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(token);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken != null && !storedToken.IsRevoked)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Revoked refresh token for user {UserId}", storedToken.UserId);
        }
    }

    private async Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken, Guid? familyId = null)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            FamilyId = familyId,
            DeviceInfo = null,
            IpAddress = null
        };

        dbContext.RefreshTokens.Add(storedToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private static string HashRefreshToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyRefreshToken(string token, string hash)
    {
        var tokenHash = HashRefreshToken(token);
        var tokenBytes = Encoding.UTF8.GetBytes(tokenHash);
        var hashBytes = Encoding.UTF8.GetBytes(hash);
        return CryptographicOperations.FixedTimeEquals(tokenBytes, hashBytes);
    }
}