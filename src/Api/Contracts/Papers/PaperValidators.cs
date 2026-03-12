using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Domain.Enums;
using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Papers;

public sealed class PaperQueryRequestValidator : AbstractValidator<PaperQueryRequest>
{
    public PaperQueryRequestValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Source).Must(ValidatorHelpers.BeValidEnum<PaperSource>).When(x => !string.IsNullOrWhiteSpace(x.Source));
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<PaperStatus>).When(x => !string.IsNullOrWhiteSpace(x.Status));
        RuleFor(x => x.SortDirection)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Equals("asc", StringComparison.OrdinalIgnoreCase) || value.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}

public sealed class CreatePaperRequestValidator : AbstractValidator<CreatePaperRequest>
{
    public CreatePaperRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.Authors).NotNull();
        RuleForEach(x => x.Authors).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Year).InclusiveBetween(1800, DateTime.UtcNow.Year + 1).When(x => x.Year.HasValue);
        RuleFor(x => x.CitationCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Source).Must(ValidatorHelpers.BeValidEnum<PaperSource>).WithMessage("Source must be a valid paper source.");
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<PaperStatus>).WithMessage("Status must be a valid paper status.");
    }
}

public sealed class UpdatePaperRequestValidator : AbstractValidator<UpdatePaperRequest>
{
    public UpdatePaperRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(1024).When(x => x.Title is not null);
        RuleForEach(x => x.Authors!).NotEmpty().MaximumLength(256).When(x => x.Authors is not null);
        RuleFor(x => x.Year).InclusiveBetween(1800, DateTime.UtcNow.Year + 1).When(x => x.Year.HasValue);
        RuleFor(x => x.CitationCount).GreaterThanOrEqualTo(0).When(x => x.CitationCount.HasValue);
        RuleFor(x => x.Status).Must(ValidatorHelpers.BeValidEnum<PaperStatus>).When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public sealed class ImportPapersRequestValidator : AbstractValidator<ImportPapersRequest>
{
    public ImportPapersRequestValidator()
    {
        RuleFor(x => x.Queries).NotEmpty();
        RuleForEach(x => x.Queries).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}
