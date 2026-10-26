using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.RoleNameRequired)
            .MaximumLength(100);

        RuleFor(x => x.Description).MaximumLength(500);

        // A role with no permission is a legitimate starting point -- it can be filled in
        // afterwards -- but a blank name in the list is a mistake, not an empty role.
        RuleForEach(x => x.Permissions)
            .NotEmpty().WithMessage(ValidationMessages.RolePermissionNameRequired);
    }
}
