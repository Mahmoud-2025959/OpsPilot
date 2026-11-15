using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Postmortems.Commands.UpdatePostmortem;

public sealed class UpdatePostmortemCommandValidator : AbstractValidator<UpdatePostmortemCommand>
{
    public UpdatePostmortemCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationMessages.PostmortemTitleRequired)
            .MaximumLength(300);

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage(ValidationMessages.PostmortemSummaryRequired)
            .MaximumLength(8000);

        RuleFor(x => x.RootCause).MaximumLength(8000);

        RuleFor(x => x.ActionItems).MaximumLength(8000);
    }
}
