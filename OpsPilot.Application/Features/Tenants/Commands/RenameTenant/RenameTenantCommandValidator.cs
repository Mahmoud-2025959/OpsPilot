using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Tenants.Commands.RenameTenant;

public sealed class RenameTenantCommandValidator : AbstractValidator<RenameTenantCommand>
{
    public RenameTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.CompanyNameRequired)
            .MaximumLength(200);
    }
}
