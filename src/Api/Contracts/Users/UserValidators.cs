using AutonomousResearchAgent.Api.Contracts.Common;
using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Users;

public sealed class UserQueryRequestValidator : AbstractValidator<UserQueryRequest>
{
    public UserQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Username).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Username));
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(128);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(64);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Username).MinimumLength(3).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.Username));
    }
}

public sealed class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    public AssignRolesRequestValidator()
    {
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(64);
    }
}
