using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Fans a domain event out to the users it concerns, honouring each user's channel
/// preferences (plan, FR-16 and UC-13).
/// </summary>
/// <remarks>
/// <para>
/// Nothing in the incident flows writes a notification by hand; they name the recipients and
/// the kind of event, and this service decides which channels actually deliver. That is what
/// keeps the preference rule — absent row means enabled — enforced in exactly one place.
/// </para>
/// <para>
/// The tenant comes from the ambient scope, like the audit writer: a notification is always
/// filed inside a unit of work that has already resolved to one company.
/// </para>
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// Queues one notification for every distinct user in <paramref name="userIds"/>.
    /// </summary>
    /// <remarks>
    /// In-app rows are marked sent immediately — there is no delivery step between the write
    /// and the user seeing them. Email rows stay pending until a mailer exists to send them.
    /// </remarks>
    Task NotifyAsync(
        IReadOnlyCollection<Guid> userIds,
        NotificationType type,
        Severity severity,
        string title,
        string body,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default);
}
