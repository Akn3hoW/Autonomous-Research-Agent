using System.Security.Cryptography;
using System.Text;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Users;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class UserService(
    ApplicationDbContext dbContext,
    ILogger<UserService> logger) : IUserService
{
    public async Task<PagedResult<UserModel>> ListAsync(UserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var users = dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            users = users.Where(u => u.Email.Contains(query.Email));
        }

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            users = users.Where(u => u.Username.Contains(query.Username));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        var totalCount = await users.LongCountAsync(cancellationToken);
        var entities = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(u => new UserModel(u.Id, u.Email, u.Username, u.IsActive, u.UserRoles.Select(ur => ur.Role.Name).ToList(), u.CreatedAt, u.UpdatedAt)).ToList();
        return new PagedResult<UserModel>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<UserModel> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        return new UserModel(user.Id, user.Email, user.Username, user.IsActive, user.UserRoles.Select(ur => ur.Role.Name).ToList(), user.CreatedAt, user.UpdatedAt);
    }

    public async Task<UserModel> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await dbContext.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
        {
            throw new ConflictException($"User with email '{command.Email}' already exists.");
        }

        if (await dbContext.Users.AnyAsync(u => u.Username == command.Username, cancellationToken))
        {
            throw new ConflictException($"User with username '{command.Username}' already exists.");
        }

        var user = new User
        {
            Email = command.Email,
            Username = command.Username,
            PasswordHash = HashPassword(command.Password)
        };

        var validRoles = await dbContext.Roles
            .Where(r => command.Roles.Contains(r.Name))
            .ToListAsync(cancellationToken);

        foreach (var role in validRoles)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created user {UserId} with {RoleCount} roles", user.Id, validRoles.Count);

        return new UserModel(user.Id, user.Email, user.Username, user.IsActive, user.UserRoles.Select(ur => ur.Role.Name).ToList(), user.CreatedAt, user.UpdatedAt);
    }

    public async Task<UserModel> UpdateAsync(Guid id, UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        if (!string.IsNullOrWhiteSpace(command.Email) && command.Email != user.Email)
        {
            if (await dbContext.Users.AnyAsync(u => u.Email == command.Email && u.Id != id, cancellationToken))
            {
                throw new ConflictException($"User with email '{command.Email}' already exists.");
            }
            user.Email = command.Email;
        }

        if (!string.IsNullOrWhiteSpace(command.Username) && command.Username != user.Username)
        {
            if (await dbContext.Users.AnyAsync(u => u.Username == command.Username && u.Id != id, cancellationToken))
            {
                throw new ConflictException($"User with username '{command.Username}' already exists.");
            }
            user.Username = command.Username;
        }

        if (command.IsActive.HasValue)
        {
            user.IsActive = command.IsActive.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated user {UserId}", user.Id);

        return new UserModel(user.Id, user.Email, user.Username, user.IsActive, user.UserRoles.Select(ur => ur.Role.Name).ToList(), user.CreatedAt, user.UpdatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted user {UserId}", id);
    }

    public async Task<UserModel> AssignRolesAsync(Guid id, AssignRolesCommand command, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var validRoles = await dbContext.Roles
            .Where(r => command.Roles.Contains(r.Name))
            .ToListAsync(cancellationToken);

        var existingRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        var targetRoleIds = validRoles.Select(r => r.Id).ToHashSet();

        var toRemove = user.UserRoles.Where(ur => !targetRoleIds.Contains(ur.RoleId)).ToList();
        foreach (var ur in toRemove)
        {
            user.UserRoles.Remove(ur);
        }

        foreach (var role in validRoles)
        {
            if (!existingRoleIds.Contains(role.Id))
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Assigned roles to user {UserId}", user.Id);

        return new UserModel(user.Id, user.Email, user.Username, user.IsActive, user.UserRoles.Select(ur => ur.Role.Name).ToList(), user.CreatedAt, user.UpdatedAt);
    }

    public async Task<IEnumerable<UserApiKeyModel>> GetApiKeysAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = await dbContext.UserApiKeys
            .Where(k => k.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return keys.Select(k => new UserApiKeyModel(k.Id, k.Name, k.Permissions, k.CreatedAt, k.ExpiresAt, k.LastUsedAt));
    }

    public async Task<(UserApiKeyModel ApiKey, string RawKey)> CreateApiKeyAsync(Guid userId, string name, string? permissions, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
    {
        var rawKey = GenerateRawKey();
        var keyHash = ComputeHash(rawKey);

        var apiKey = new UserApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = keyHash,
            Permissions = permissions ?? string.Empty,
            ExpiresAt = expiresAt
        };

        dbContext.UserApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created API key {KeyId} for user {UserId}", apiKey.Id, userId);

        return (new UserApiKeyModel(apiKey.Id, apiKey.Name, apiKey.Permissions, apiKey.CreatedAt, apiKey.ExpiresAt, apiKey.LastUsedAt), rawKey);
    }

    public async Task DeleteApiKeyAsync(Guid userId, Guid keyId, CancellationToken cancellationToken)
    {
        var apiKey = await dbContext.UserApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("ApiKey", keyId);

        dbContext.UserApiKeys.Remove(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted API key {KeyId} for user {UserId}", keyId, userId);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeHash(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}