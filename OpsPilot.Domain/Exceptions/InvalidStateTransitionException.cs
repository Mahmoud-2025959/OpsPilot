namespace OpsPilot.Domain.Exceptions;

/// <summary>
/// Raised when an aggregate is asked to move to a state its rules forbid.
/// </summary>
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string message)
        : base(message, "invalid_state_transition")
    {
    }

    /// <summary>Builds the exception from the rejected transition itself.</summary>
    public InvalidStateTransitionException(Enums.IncidentStatus from, Enums.IncidentStatus to)
        : base($"Cannot move incident from status '{from}' to '{to}'.", "invalid_state_transition")
    {
        From = from;
        To = to;
    }

    public Enums.IncidentStatus? From { get; }

    public Enums.IncidentStatus? To { get; }
}
