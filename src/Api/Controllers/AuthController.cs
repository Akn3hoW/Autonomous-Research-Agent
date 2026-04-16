using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Auth;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/auth")]
[EnableRateLimiting(RateLimiterPolicyNames.Strict)]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.ToCommand(), cancellationToken);
        return Ok(result.ToResponse());
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(Login), result.ToResponse());
    }

    [HttpPost("token-refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] TokenRefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(request.ToCommand(), cancellationToken);
        return Ok(result.ToResponse());
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Headers["X-Refresh-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("revoke-all-tokens")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RevokeAllTokens(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userId, out var id))
        {
            await authService.RevokeAllUserTokensAsync(id, cancellationToken);
        }
        return NoContent();
    }
}

public static class AuthMappingExtensions
{
    public static LoginCommand ToCommand(this LoginRequest request) =>
        new(request.Email, request.Password);

    public static RegisterCommand ToCommand(this RegisterRequest request) =>
        new(request.Email, request.Username, request.Password);

    public static TokenRefreshCommand ToCommand(this TokenRefreshRequest request) =>
        new(request.RefreshToken);

    public static AuthResponse ToResponse(this AuthResult result) =>
        new(
            result.AccessToken,
            result.RefreshToken,
            result.ExpiresAt,
            new UserResponse(result.User.Id, result.User.Email, result.User.Username, result.User.Roles));
}