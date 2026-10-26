using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Webhooks.Commands.CreateWebhook;

public sealed class CreateWebhookCommandValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);
        RuleFor(x => x.Url).NotEmpty().WithMessage(ValidationMessages.WebhookUrlRequired).MaximumLength(500);
        RuleFor(x => x.Url).Must(BeDeliverable).WithMessage(ValidationMessages.WebhookUrlInvalid);
    }

    /// <summary>
    /// The destination has to be an absolute URL, and it has to be one the signing secret can
    /// safely travel over: HTTPS, unless it points at the machine the caller is on — a local
    /// integration has no use for a certificate, and refusing it would only push people to
    /// disable the check.
    /// </summary>
    private static bool BeDeliverable(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttp && IsLocal(uri);
    }

    private static bool IsLocal(Uri uri) =>
        uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}
