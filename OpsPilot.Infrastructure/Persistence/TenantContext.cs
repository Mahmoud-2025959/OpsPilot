using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Scoped ambient tenant. Registered per request, so the value is naturally isolated and
/// safe to capture from the EF global query filter.
/// </summary>
public sealed class TenantContext : ICurrentTenant
{
    private Guid? _tenantId;

    public Guid TenantId => _tenantId ?? Guid.Empty;

    public bool IsResolved => _tenantId is { } id && id != Guid.Empty;

    public IDisposable Enter(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        }

        var previous = _tenantId;
        _tenantId = tenantId;
        return new Scope(this, previous);
    }

    private void Restore(Guid? previous)
    {
        _tenantId = previous;
    }

    private sealed class Scope(TenantContext owner, Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            owner.Restore(previous);
            _disposed = true;
        }
    }
}
