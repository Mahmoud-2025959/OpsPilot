using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Postmortems.Commands.CreatePostmortem;

public sealed class CreatePostmortemCommandValidator : AbstractValidator<CreatePostmortemCommand>
{
    public CreatePostmortemCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.IncidentId).NotEmpty();

        RuleFor(x => x.AuthorId).NotEmpty();

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
