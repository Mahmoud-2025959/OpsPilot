using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Roles.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);
        RuleFor(x => x.Id).NotEmpty();

        // An empty list is a valid answer: it strips the role of everything it granted.
        RuleForEach(x => x.Permissions)
            .NotEmpty().WithMessage(ValidationMessages.RolePermissionNameRequired);
    }
}
