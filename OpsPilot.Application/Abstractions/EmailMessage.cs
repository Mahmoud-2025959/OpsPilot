namespace OpsPilot.Application.Abstractions;

/// <summary>One outbound mail: a single recipient, a subject, and a plain-text body.</summary>
/// <remarks>
/// Plain text on purpose: notification bodies are written as sentences, and a message that has
/// to reach somebody during an incident must still make sense where HTML is stripped - a mail
/// client in text mode, a gateway that rewrites payloads, a person reading it on a terminal.
/// </remarks>
public sealed record EmailMessage(string To, string Subject, string Body);
