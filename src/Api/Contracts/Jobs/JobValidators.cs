using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Domain.Enums;
using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Jobs;

public sealed class JobQueryRequestValidator : AbstractValidator<JobQueryRequest>
{
    public JobQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Type).Must(ValidatorHelpers.BeValidEnum<JobType>)
            .When(x => !string.IsNullOrWhiteSpace(x.Type))
            .WithMessage("Type must be a valid job type.");
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<JobStatus>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be a valid job status.");
    }
}

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Type).Must(ValidatorHelpers.BeValidEnum<JobType>)
            .WithMessage("Type must be a valid job type.");
    }
}

public sealed class CreateImportJobRequestValidator : AbstractValidator<CreateImportJobRequest>
{
    public CreateImportJobRequestValidator()
    {
        RuleFor(x => x.Queries).NotEmpty();
        RuleForEach(x => x.Queries).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class CreateSummarizeJobRequestValidator : AbstractValidator<CreateSummarizeJobRequest>
{
    public CreateSummarizeJobRequestValidator()
    {
        RuleFor(x => x.PaperId).NotEmpty();
        RuleFor(x => x.ModelName).NotEmpty().MaximumLength(256).Equal("openrouter/hunter-alpha").WithMessage("Only openrouter/hunter-alpha is supported for automated summarization jobs.");
        RuleFor(x => x.PromptVersion).NotEmpty().MaximumLength(128);
    }
}

public sealed class RetryJobRequestValidator : AbstractValidator<RetryJobRequest>
{
    public RetryJobRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(1024).When(x => x.Reason is not null);
    }
}

