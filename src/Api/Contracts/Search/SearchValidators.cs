using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Search;

public sealed class SearchRequestValidator : AbstractValidator<SearchRequest>
{
    public SearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(512);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SemanticSearchRequestValidator : AbstractValidator<SemanticSearchRequest>
{
    public SemanticSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(512);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MaxCandidates).InclusiveBetween(1, 500);
    }
}

public sealed class HybridSearchRequestValidator : AbstractValidator<HybridSearchRequest>
{
    public HybridSearchRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(512);
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MaxCandidates).InclusiveBetween(1, 500);
        RuleFor(x => x.KeywordWeight).InclusiveBetween(0, 1);
        RuleFor(x => x.SemanticWeight).InclusiveBetween(0, 1);
        RuleFor(x => x)
            .Must(x => x.KeywordWeight + x.SemanticWeight > 0)
            .WithMessage("KeywordWeight and SemanticWeight must add up to more than zero.");
    }
}

