using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Events.Commands.IngestEvent;

public sealed class IngestEventCommandValidator : AbstractValidator<IngestEventCommand>
{
    public IngestEventCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);
        RuleFor(x => x.ApiKeyId).NotEmpty();
        RuleFor(x => x.ReportedBy).NotEmpty();

        RuleFor(x => x.Service)
            .NotEmpty().WithMessage(ValidationMessages.IngestedServiceRequired)
            .MaximumLength(200);

        RuleFor(x => x.Environment).IsInEnum().WithMessage(ValidationMessages.ServiceEnvironmentInvalid);

        RuleFor(x => x.Severity).IsInEnum().WithMessage(ValidationMessages.IngestedSeverityInvalid);

        // Incident.Description caps at 2000, and the message is also the incident title, so this
        // is the natural ceiling for the whole payload.
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(ValidationMessages.IngestedMessageRequired)
            .MaximumLength(2000);
    }
}
