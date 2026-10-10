namespace OpsPilot.Domain.Exceptions;

/// <summary>
/// Raised when something attempts to update or delete an append-only record such as an
/// audit log entry or an incident timeline event (ERD §1).
/// </summary>
public sealed class AppendOnlyEntityModifiedException(string message) : DomainException(message, "append_only_violation");
