using OpsPilot.Application.Abstractions;

namespace OpsPilot.IntegrationTests;

/// <summary>A transport that never succeeds, so a test can watch a row use up its attempts.</summary>
internal sealed class FailingEmailSender : IEmailSender
{
    public int Attempts { get; private set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Attempts++;

        throw new InvalidOperationException("The mail server is unreachable.");
    }
}
