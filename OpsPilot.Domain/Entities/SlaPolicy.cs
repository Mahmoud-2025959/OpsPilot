using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// Response and resolution targets for one severity level within a tenant.
/// </summary>
/// <remarks>
/// Seeded with the default values when a tenant is provisioned; one row per severity
/// is enforced by a unique index on <c>(TenantId, Severity)</c>.
/// </remarks>
public sealed class SlaPolicy : TenantOwnedEntity
{
    private SlaPolicy(Severity severity, int responseMinutes, int resolutionMinutes)
    {
        Severity = severity;
        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
    }

    public Severity Severity { get; private set; }

    public int ResponseMinutes { get; private set; }

    public int ResolutionMinutes { get; private set; }

    public static SlaPolicy Create(Guid tenantId, Severity severity, int responseMinutes, int resolutionMinutes)
    {
        if (responseMinutes <= 0 || resolutionMinutes <= 0)
        {
            throw new DomainException("SLA durations must be positive.");
        }

        if (resolutionMinutes <= responseMinutes)
        {
            throw new DomainException("Resolution SLA must be greater than response SLA.");
        }

        return new SlaPolicy(severity, responseMinutes, resolutionMinutes)
        {
            TenantId = tenantId
        };
    }

    /// <summary>Retunes the targets, keeping the invariant that resolution trails response.</summary>
    public void Retune(int responseMinutes, int resolutionMinutes)
    {
        if (responseMinutes <= 0 || resolutionMinutes <= 0)
        {
            throw new DomainException("SLA durations must be positive.");
        }

        if (resolutionMinutes <= responseMinutes)
        {
            throw new DomainException("Resolution SLA must be greater than response SLA.");
        }

        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
    }

    public TimeSpan ResponseWindow => TimeSpan.FromMinutes(ResponseMinutes);

    public TimeSpan ResolutionWindow => TimeSpan.FromMinutes(ResolutionMinutes);
}
