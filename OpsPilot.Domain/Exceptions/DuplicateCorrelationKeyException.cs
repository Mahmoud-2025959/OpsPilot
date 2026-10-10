namespace OpsPilot.Domain.Exceptions;

public sealed class DuplicateCorrelationKeyException : DomainException
{
    public DuplicateCorrelationKeyException(string? correlationKey, Guid tenantId)
        : base($"An incident with correlation key '{correlationKey}' already exists for tenant '{tenantId}'.",
            "duplicate_correlation_key")
    {
    }
}