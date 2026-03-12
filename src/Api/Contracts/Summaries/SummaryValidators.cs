using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Domain.Enums;
using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed class CreateSummaryRequestValidator : AbstractValidator<CreateSummaryRequest>
{
    public CreateSummaryRequestValidator()
    {
        RuleFor(x => x.ModelName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PromptVersion).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<SummaryStatus>)
            .WithMessage("Status must be a valid summary status.");
    }
}

public sealed class UpdateSummaryRequestValidator : AbstractValidator<UpdateSummaryRequest>
{
    public UpdateSummaryRequestValidator()
    {
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<SummaryStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be a valid summary status.");
    }
}

public sealed class ReviewSummaryRequestValidator : AbstractValidator<ReviewSummaryRequest>
{
    public ReviewSummaryRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

