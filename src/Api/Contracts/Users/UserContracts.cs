namespace AutonomousResearchAgent.Api.Contracts.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class UserQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Email { get; init; }
    public string? Username { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record CreateUserRequest(
    string Email,
    string Username,
    string Password,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserRequest(
    string? Email,
    string? Username,
    bool? IsActive);

public sealed record AssignRolesRequest(
    IReadOnlyCollection<string> Roles);

public sealed record CreateApiKeyRequest(
    string Name,
    string? Permissions,
    DateTimeOffset? ExpiresAt);

public sealed record UserApiKeyDto(
    Guid Id,
    string Name,
    string? Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    string? RawKey = null);
