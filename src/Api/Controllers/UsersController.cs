using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Users;
using AutonomousResearchAgent.Api.Extensions;
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
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToDto());
    }

    [HttpPut("{id:guid}")]
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
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        await userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> AssignRoles(Guid id, [FromBody] AssignRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.AssignRolesAsync(id, request.ToCommand(), cancellationToken);
        return Ok(user.ToDto());
    }
}

public static class UserMappingExtensions
{
    public static UserQuery ToApplicationModel(this UserQueryRequest request) =>
        new(request.PageNumber, request.PageSize, request.Email, request.Username, request.IsActive);

    public static CreateUserCommand ToCommand(this CreateUserRequest request) =>
        new(request.Email, request.Username, request.Password, request.Roles);

    public static UpdateUserCommand ToCommand(this UpdateUserRequest request) =>
        new(request.Email, request.Username, request.IsActive);

    public static AssignRolesCommand ToCommand(this AssignRolesRequest request) =>
        new(request.Roles);

    public static UserDto ToDto(this UserModel model) =>
        new(model.Id, model.Email, model.Username, model.IsActive, model.Roles, model.CreatedAt, model.UpdatedAt);
}
