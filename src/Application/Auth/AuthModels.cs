namespace AutonomousResearchAgent.Application.Auth;

public sealed record LoginCommand(
    string Email,
    string Password);

public sealed record RegisterCommand(
    string Email,
    string Username,
    string Password);

public sealed record TokenRefreshCommand(
    string RefreshToken);

public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    IReadOnlyCollection<string> Roles);
