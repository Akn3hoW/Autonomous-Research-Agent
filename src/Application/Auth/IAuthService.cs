namespace AutonomousResearchAgent.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);
    Task<AuthResult> RefreshTokenAsync(TokenRefreshCommand command, CancellationToken cancellationToken);
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> DetectCompromisedTokenAsync(string token, string currentIpAddress, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken);
}