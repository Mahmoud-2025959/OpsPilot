using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage(ValidationMessages.EmailRequired);
        RuleFor(x => x.Password).NotEmpty().WithMessage(ValidationMessages.PasswordRequired);
    }
}
