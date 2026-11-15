using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Services.Commands.CreateService;

public sealed class CreateServiceCommandValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.ServiceNameRequired)
            .MaximumLength(200);

        RuleFor(x => x.Environment)
            .IsInEnum().WithMessage(ValidationMessages.ServiceEnvironmentInvalid);

        // Optional: when omitted the handler derives a handle from the name, the same way
        // companies do.
        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage(ValidationMessages.ServiceSlugInvalid)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.RepositoryUrl).MaximumLength(500);
    }
}
