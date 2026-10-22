namespace OpsPilot.Contracts.Realtime;

/// <summary>
/// A message one watcher sent to the others watching the same company over the live channel
/// (plan, FR-20).
/// </summary>
/// <remarks>
/// The sender rides along even though the hub already knows who is connected: the receiving
/// client has to render who said it, and re-deriving that from a second lookup would give the
/// two ends two chances to disagree about it.
/// </remarks>
public sealed record ConversationMessageDto(
    Guid SenderId,
    string Text,
    DateTimeOffset SentAt);
