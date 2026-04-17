using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserModel>> ListAsync(UserQuery query, CancellationToken cancellationToken);
    Task<UserModel> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserModel> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken);
    Task<UserModel> UpdateAsync(Guid id, UpdateUserCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<UserModel> AssignRolesAsync(Guid id, AssignRolesCommand command, CancellationToken cancellationToken);
    Task<IEnumerable<UserApiKeyModel>> GetApiKeysAsync(Guid userId, CancellationToken cancellationToken);
    Task<(UserApiKeyModel ApiKey, string RawKey)> CreateApiKeyAsync(Guid userId, string name, string? permissions, DateTimeOffset? expiresAt, CancellationToken cancellationToken);
    Task DeleteApiKeyAsync(Guid userId, Guid keyId, CancellationToken cancellationToken);
}