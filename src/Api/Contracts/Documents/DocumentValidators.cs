using FluentValidation;

namespace AutonomousResearchAgent.Api.Contracts.Documents;

public sealed class CreatePaperDocumentRequestValidator : AbstractValidator<CreatePaperDocumentRequest>
{
    public CreatePaperDocumentRequestValidator()
    {
        RuleFor(x => x.SourceUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeSupportedSourceUrl)
            .WithMessage("SourceUrl must be a valid absolute http or https URL.");

        RuleFor(x => x.FileName)
            .MaximumLength(512)
            .When(x => x.FileName is not null);

        RuleFor(x => x.MediaType)
            .MaximumLength(256)
            .When(x => x.MediaType is not null);
    }

    private static bool BeSupportedSourceUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}

public sealed class QueuePaperDocumentProcessingRequestValidator : AbstractValidator<QueuePaperDocumentProcessingRequest>
{
}
