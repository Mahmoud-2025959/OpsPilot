using FluentValidation;

namespace OpsPilot.Application.Features.Incidents.Commands.AssignIncident;

public sealed class AssignIncidentCommandValidator : AbstractValidator<AssignIncidentCommand>
{
    public AssignIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.AssigneeId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
    }
}
