using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Auth.Commands.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage(ValidationMessages.CompanyNameRequired)
            .MaximumLength(200);

        // Optional: when omitted the provisioner derives a unique slug from the company name.
        // When supplied it must be a URL-safe handle.
        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage(ValidationMessages.SlugInvalid)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(ValidationMessages.EmailRequired)
            .EmailAddress().WithMessage(ValidationMessages.EmailInvalid);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationMessages.DisplayNameRequired)
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(ValidationMessages.PasswordRequired)
            .MinimumLength(8).WithMessage(ValidationMessages.PasswordTooShort);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage(ValidationMessages.ConfirmPasswordRequired)
            .Equal(x => x.Password).WithMessage(ValidationMessages.PasswordsDoNotMatch);
    }
}
