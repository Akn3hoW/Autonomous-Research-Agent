using AutonomousResearchAgent.Api.Contracts.Auth;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/auth")]
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
