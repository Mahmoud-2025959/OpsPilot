using OpsPilot.Application.Abstractions;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Captures what the sweep handed to the transport, for tests that drive a pass directly
/// instead of standing up an SMTP server.
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
