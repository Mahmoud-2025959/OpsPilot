using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Identity;

/// <summary>One default SLA target, per severity.</summary>
public sealed record SlaPolicyDefault(Severity Severity, int ResponseMinutes, int ResolutionMinutes);

/// <summary>
/// The response and resolution targets seeded for every new tenant (ERD §3, "SLA والإشعارات").
/// </summary>
/// <remarks>
/// One row per severity; the database enforces that with a unique index on
/// <c>(TenantId, Severity)</c>. The values are the starting point an Admin then tunes.
/// </remarks>
public static class DefaultSlaPolicies
{
    public static IReadOnlyList<SlaPolicyDefault> All { get; } =
    [
        new(Severity.Critical, ResponseMinutes: 15, ResolutionMinutes: 240),
        new(Severity.High, ResponseMinutes: 30, ResolutionMinutes: 480),
        new(Severity.Medium, ResponseMinutes: 120, ResolutionMinutes: 1440),
        new(Severity.Low, ResponseMinutes: 480, ResolutionMinutes: 4320)
    ];
}
