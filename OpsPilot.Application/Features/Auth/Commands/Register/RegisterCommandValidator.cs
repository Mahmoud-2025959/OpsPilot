using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);
        RuleFor(x => x.Email).NotEmpty().WithMessage(ValidationMessages.EmailRequired)
            .EmailAddress().WithMessage(ValidationMessages.EmailInvalid);
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage(ValidationMessages.DisplayNameRequired)
            .MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().WithMessage(ValidationMessages.PasswordRequired)
            .MinimumLength(6).WithMessage(ValidationMessages.PasswordTooShort);
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage(ValidationMessages.ConfirmPasswordRequired)
            .Equal(x => x.Password).WithMessage(ValidationMessages.PasswordsDoNotMatch);
    }
}
