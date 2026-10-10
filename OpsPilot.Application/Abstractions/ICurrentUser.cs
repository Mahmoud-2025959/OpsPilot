namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Exposes the identity of the user behind the current unit of work, for audit stamping.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Stable identifier of the acting user, or null for system actions such as SLA sweeps.
    /// </summary>
    string? UserId { get; }
}
