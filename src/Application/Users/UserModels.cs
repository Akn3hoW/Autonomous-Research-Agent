using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Users;

public sealed record UserModel(
    Guid Id,
    string Email,
    string Username,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UserQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string? Email = null,
    string? Username = null,
    bool? IsActive = null);

public sealed record CreateUserCommand(
    string Email,
    string Username,
    string Password,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserCommand(
    string? Email,
    string? Username,
    bool? IsActive);

public sealed record AssignRolesCommand(
    IReadOnlyCollection<string> Roles);

public sealed record UserApiKeyModel(
    Guid Id,
    string Name,
    string? Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record PagedUsersResult(PagedResult<UserModel> Result);