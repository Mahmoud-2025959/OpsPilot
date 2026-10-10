using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Auth.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(ValidationMessages.CurrentPasswordRequired);
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(ValidationMessages.NewPasswordRequired)
            .MinimumLength(6).WithMessage(ValidationMessages.PasswordTooShort);
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage(ValidationMessages.ConfirmPasswordRequired)
            .Equal(x => x.NewPassword).WithMessage(ValidationMessages.PasswordsDoNotMatch);
    }
}
