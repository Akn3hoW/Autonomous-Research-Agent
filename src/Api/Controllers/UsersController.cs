using System.Security.Cryptography;
using System.Text;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Users;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(PagedResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserDto>>> GetUsers([FromQuery] UserQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.ListAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await userService.GetByIdAsync(id, cancellationToken);
        return Ok(user.ToDto());
    }

    [HttpPost]
    [Audited]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToDto());
    }

    [HttpPut("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.UpdateAsync(id, request.ToCommand(), cancellationToken);
        return Ok(user.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        await userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [Audited]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> AssignRoles(Guid id, [FromBody] AssignRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.AssignRolesAsync(id, request.ToCommand(), cancellationToken);
        return Ok(user.ToDto());
    }

    [HttpGet("api-keys")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<UserApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserApiKeyDto>>> GetApiKeys(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
            return Unauthorized();

        var keys = await userService.GetApiKeysAsync(id, cancellationToken);
        return Ok(keys.Select(k => k.ToDto()));
    }

    [HttpPost("api-keys")]
    [Audited]
    [Authorize]
    [ProducesResponseType(typeof(UserApiKeyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserApiKeyDto>> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
            return Unauthorized();

        var (apiKey, keyHash) = await userService.CreateApiKeyAsync(id, request.Name, request.Permissions, request.ExpiresAt, cancellationToken);
        return CreatedAtAction(nameof(GetApiKeys), new { id }, apiKey.ToDto(keyHash));
    }

    [HttpDelete("api-keys/{keyId:guid}")]
    [Audited]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteApiKey(Guid keyId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
            return Unauthorized();

        await userService.DeleteApiKeyAsync(id, keyId, cancellationToken);
        return NoContent();
    }
}

public static class UserMappingExtensions
{
    public static UserQuery ToApplicationModel(this UserQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UserQuery(request.PageNumber, request.PageSize, request.Email, request.Username, request.IsActive);
    }

    public static CreateUserCommand ToCommand(this CreateUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateUserCommand(request.Email, request.Username, request.Password, request.Roles);
    }

    public static UpdateUserCommand ToCommand(this UpdateUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateUserCommand(request.Email, request.Username, request.IsActive);
    }

    public static AssignRolesCommand ToCommand(this AssignRolesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AssignRolesCommand(request.Roles);
    }

    public static UserDto ToDto(this UserModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new UserDto(model.Id, model.Email, model.Username, model.IsActive, model.Roles, model.CreatedAt, model.UpdatedAt);
    }

    public static UserApiKeyDto ToDto(this UserApiKeyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new UserApiKeyDto(model.Id, model.Name, model.Permissions, model.CreatedAt, model.ExpiresAt, model.LastUsedAt);
    }

    public static UserApiKeyDto ToDto(this UserApiKeyModel model, string rawKey)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new UserApiKeyDto(model.Id, model.Name, model.Permissions, model.CreatedAt, model.ExpiresAt, model.LastUsedAt, rawKey);
    }
}