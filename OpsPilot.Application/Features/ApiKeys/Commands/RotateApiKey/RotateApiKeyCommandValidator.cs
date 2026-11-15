using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.ApiKeys.Commands.RotateApiKey;

public sealed class RotateApiKeyCommandValidator : AbstractValidator<RotateApiKeyCommand>
{
    public RotateApiKeyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationMessages.ApiKeyIdRequired);
    }
}
