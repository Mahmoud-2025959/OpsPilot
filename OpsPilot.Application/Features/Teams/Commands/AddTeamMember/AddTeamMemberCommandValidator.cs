using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Teams.Commands.AddTeamMember;

public sealed class AddTeamMemberCommandValidator : AbstractValidator<AddTeamMemberCommand>
{
    public AddTeamMemberCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty().WithMessage(ValidationMessages.TeamMemberUserIdRequired);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage(ValidationMessages.TeamMemberRoleInvalid)
            .When(x => x.Role.HasValue);
    }
}
