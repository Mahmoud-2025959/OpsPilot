using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Services.Commands.UpdateService;

public sealed class UpdateServiceCommandValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.ServiceNameRequired)
            .MaximumLength(200);

        RuleFor(x => x.Environment)
            .IsInEnum().WithMessage(ValidationMessages.ServiceEnvironmentInvalid);

        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage(ValidationMessages.ServiceSlugInvalid)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Slug));

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.RepositoryUrl).MaximumLength(500);
    }
}
