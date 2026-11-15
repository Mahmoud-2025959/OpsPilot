namespace OpsPilot.Infrastructure;

/// <summary>SMTP transport settings for the email channel (plan, notifications group).</summary>
/// <remarks>
/// The credentials on this object are the one piece of configuration that must never be
/// committed: they bind from <c>Smtp</c> in configuration, which in development comes from
/// user-secrets and in deployment from the environment. The defaults here are inert - an
/// unconfigured installation simply never sends.
/// </remarks>
public sealed class SmtpOptions
{
    /// <summary>Configuration section these bind from.</summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// The transport's master switch, checked before any attempt is made. Off means queued
    /// emails keep waiting without burning attempts, so switching the transport on later
    /// delivers everything that piled up meanwhile.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>SMTP host, e.g. <c>smtp.gmail.com</c>.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>587 is the submission port and what STARTTLS expects.</summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// STARTTLS rather than implicit TLS: submission on 587 starts in the clear and upgrades,
    /// and refusing to send without that upgrade is the correct posture for incident mail.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Account the mail is sent as. For Gmail this is the full address.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Never a literal: in development this comes from user-secrets, in deployment from the
    /// environment. For Gmail it is an App Password, which only exists once 2-Step
    /// Verification is on.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Envelope sender - the address bounces and errors come back to.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>What recipients see as the sender's name.</summary>
    public string FromName { get; set; } = "OpsPilot";
}
