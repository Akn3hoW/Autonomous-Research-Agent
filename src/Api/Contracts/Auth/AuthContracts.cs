namespace AutonomousResearchAgent.Api.Contracts.Auth;

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password);

public sealed record TokenRefreshRequest(
    string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string Username,
    IReadOnlyCollection<string> Roles);
