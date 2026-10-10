using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A machine-generated suggestion attached to an incident (ERD §3, optional phase 6).
/// </summary>
/// <remarks>
/// Stored as a suggestion rather than written onto the incident itself so that AI output
/// never overwrites recorded facts. A human has to accept it explicitly.
/// </remarks>
public sealed class AiSuggestion : TenantOwnedEntity
{
    private AiSuggestion(Guid incidentId, AiSuggestionKind kind, string content, string model)
    {
        IncidentId = incidentId;
        Kind = kind;
        Content = content;
        Model = model;
    }

    public Guid IncidentId { get; private set; }

    public AiSuggestionKind Kind { get; private set; }

    /// <summary>The suggested text, stored as produced.</summary>
    public string Content { get; private set; }

    /// <summary>Identifier of the model that produced the suggestion.</summary>
    public string Model { get; private set; }

    /// <summary>When a human accepted the suggestion, or null while it is still pending.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    public bool IsAccepted => AcceptedAt is not null;

    public static AiSuggestion Create(Guid tenantId, Guid incidentId, AiSuggestionKind kind, string content, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return new AiSuggestion(incidentId, kind, content.Trim(), model.Trim())
        {
            TenantId = tenantId
        };
    }

    /// <summary>Accepts the suggestion. Idempotent: re-accepting keeps the original timestamp.</summary>
    public void Accept(DateTimeOffset at)
    {
        AcceptedAt ??= at;
    }
}
