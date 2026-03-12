using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Analysis;

public sealed class ComparePapersRequestValidator : AbstractValidator<ComparePapersRequest>
{
    public ComparePapersRequestValidator()
    {
        RuleFor(x => x.LeftPaperId).NotEmpty();
        RuleFor(x => x.RightPaperId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.LeftPaperId != x.RightPaperId)
            .WithMessage("LeftPaperId and RightPaperId must be different.");
    }
}

public sealed class CompareFieldsRequestValidator : AbstractValidator<CompareFieldsRequest>
{
    public CompareFieldsRequestValidator()
    {
        RuleFor(x => x.LeftFilter).NotEmpty().MaximumLength(512);
        RuleFor(x => x.RightFilter).NotEmpty().MaximumLength(512);
    }
}

public sealed class GenerateInsightsRequestValidator : AbstractValidator<GenerateInsightsRequest>
{
    public GenerateInsightsRequestValidator()
    {
        RuleFor(x => x.Filter).NotEmpty().MaximumLength(512);
    }
}

