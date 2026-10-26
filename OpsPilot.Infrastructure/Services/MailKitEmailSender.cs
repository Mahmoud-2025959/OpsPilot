using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Services;

/// <summary>
/// Delivers one message over SMTP with MailKit (plan, notifications group: email channel).
/// </summary>
/// <remarks>
/// The transport knows nothing about queues, tenants or attempts - it is handed one envelope
/// and either puts it on the wire or throws. What a failure means is the dispatch worker's
/// call to make, because only the worker knows how many attempts a row has left.
/// </remarks>
public sealed class MailKitEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<SmtpOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            // The dispatch worker checks this before it attempts anything; the guard is for
            // anyone resolving the sender directly, so a host with no transport configured
            // fails loudly instead of reporting deliveries it never made.
            throw new InvalidOperationException(
                "SMTP delivery is switched off (Smtp:Enabled is false).");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Host, nameof(_options.Host));
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.From, nameof(_options.From));

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(_options.FromName, _options.From));
        mail.To.Add(MailboxAddress.Parse(message.To));
        mail.Subject = message.Subject;
        mail.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        }

        try
        {
            await client.SendAsync(mail, cancellationToken);
        }
        finally
        {
            try
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // The outcome above is what the caller records, so an unclean hangup on the way
                // out is logged and swallowed: failing here would turn a delivered email into a
                // recorded failure, and the next pass would send it a second time.
                _logger.LogWarning(exception, "Closing the SMTP connection failed after the send.");
            }
        }
    }
}
