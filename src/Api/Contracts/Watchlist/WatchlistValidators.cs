using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Watchlist;

public sealed class SavedSearchQueryRequestValidator : AbstractValidator<SavedSearchQueryRequest>
{
    public SavedSearchQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class NotificationQueryRequestValidator : AbstractValidator<NotificationQueryRequest>
{
    public NotificationQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
