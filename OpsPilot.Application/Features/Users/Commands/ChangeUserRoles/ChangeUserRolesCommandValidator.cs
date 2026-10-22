using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Users.Commands.ChangeUserRoles;

public sealed class ChangeUserRolesCommandValidator : AbstractValidator<ChangeUserRolesCommand>
{
    public ChangeUserRolesCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);
        RuleFor(x => x.UserId).NotEmpty();

        // Stripping every role would leave an account that can sign in but do nothing, which is
        // what deactivating is for. Requiring one role keeps the two operations distinct.
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage(ValidationMessages.UserRolesRequired);

        RuleForEach(x => x.Roles)
            .NotEmpty().WithMessage(ValidationMessages.UserRolesRequired);
    }
}
