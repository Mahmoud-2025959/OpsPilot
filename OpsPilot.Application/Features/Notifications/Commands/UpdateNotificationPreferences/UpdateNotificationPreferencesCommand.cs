using MediatR;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

/// <summary>One severity-over-channel choice, already parsed into domain values.</summary>
public sealed record NotificationPreferenceTarget(
    Severity Severity,
    NotificationChannel Channel,
    bool Enabled);

/// <summary>
/// Applies the signed-in user's notification preferences (plan, UC-13 and FR-16). The list
/// is partial: combinations that are not supplied keep whatever state they were in, so a
/// caller can save one switch or all eight without having to echo the rest back.
/// </summary>
/// <remarks>
/// The tenant is supplied by the endpoint from the ambient scope rather than trusted from the
/// body, for the same reason every other command does it: a caller cannot configure a company
/// they do not belong to.
/// </remarks>
public sealed record UpdateNotificationPreferencesCommand(
    Guid TenantId,
    Guid UserId,
    IReadOnlyList<NotificationPreferenceTarget> Targets)
    : IRequest<IReadOnlyList<NotificationPreferenceDto>>;
