using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.ApiKeyNameRequired)
            .MaximumLength(200);

        // Omitting the expiry is fine (the handler applies a default); supplying one in the past
        // is not, because the key would be born unusable.
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt > DateTimeOffset.UtcNow)
            .WithMessage(ValidationMessages.ApiKeyExpiryMustBeInTheFuture)
            .When(x => x.ExpiresAt.HasValue);
    }
}
