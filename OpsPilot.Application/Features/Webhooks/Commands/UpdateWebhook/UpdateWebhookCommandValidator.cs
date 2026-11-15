using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.Webhooks.Commands.UpdateWebhook;

public sealed class UpdateWebhookCommandValidator : AbstractValidator<UpdateWebhookCommand>
{
    public UpdateWebhookCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationMessages.WebhookIdRequired);
        RuleFor(x => x.Url).NotEmpty().WithMessage(ValidationMessages.WebhookUrlRequired).MaximumLength(500);
        RuleFor(x => x.Url).Must(BeDeliverable).WithMessage(ValidationMessages.WebhookUrlInvalid);
    }

    /// <summary>
    /// The same rule as on registration: an absolute URL, over HTTPS unless it points at the
    /// local machine. An endpoint that is being moved is moved somewhere, not nowhere.
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
