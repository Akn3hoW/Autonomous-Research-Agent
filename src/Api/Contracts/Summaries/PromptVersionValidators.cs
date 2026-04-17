using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed class CreatePromptVersionRequestValidator : AbstractValidator<CreatePromptVersionRequest>
{
    public CreatePromptVersionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(10000);
        RuleFor(x => x.UserPromptTemplate).NotEmpty().MaximumLength(10000);
    }
}

public sealed class UpdatePromptVersionRequestValidator : AbstractValidator<UpdatePromptVersionRequest>
{
    public UpdatePromptVersionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.SystemPrompt).NotEmpty().MaximumLength(10000).When(x => x.SystemPrompt is not null);
        RuleFor(x => x.UserPromptTemplate).NotEmpty().MaximumLength(10000).When(x => x.UserPromptTemplate is not null);
    }
}