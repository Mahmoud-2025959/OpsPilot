namespace OpsPilot.Application.Common.Constants;

/// <summary>
/// The kinds of integration event the outbox carries (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// A closed vocabulary rather than free strings: these names leave the process, so a typo would
/// be discovered by whoever wrote the consumer — long after the change shipped.
/// </remarks>
public static class IntegrationEventTypes
{
    /// <summary>A new incident, carrying the id of its first timeline entry.</summary>
    public const string IncidentCreated = "IncidentCreated";

    /// <summary>A new entry on an existing incident's timeline.</summary>
    public const string IncidentEventAdded = "IncidentEventAdded";
}
