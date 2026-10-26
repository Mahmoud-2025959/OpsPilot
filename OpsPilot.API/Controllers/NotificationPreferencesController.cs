using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Notifications.Commands.UpdateNotificationPreferences;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;

namespace OpsPilot.API.Controllers;

/// <summary>
/// Saves the signed-in user's notification preferences (plan, UC-13 and FR-16).
/// </summary>
/// <remarks>
/// Like the inbox itself, this needs no permission gate: a user configuring their own channel
/// preferences is not acting on company data, and the user id comes from the token. The
/// response is the full effective set so a client can re-render every switch from one call.
/// </remarks>
[Route("api/v1/notification-preferences")]
public sealed class NotificationPreferencesController : BaseController
{
    [HttpPut]
    public async Task<ActionResult<Response<IReadOnlyList<NotificationPreferenceDto>>>> Update(
        [FromBody] UpdateNotificationPreferencesRequest request)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized(Failure<IReadOnlyList<NotificationPreferenceDto>>(
                "Unauthorized.",
                BusinessCode.Unauthorized));
        }

        if (request.Preferences is null || request.Preferences.Count == 0)
        {
            return BadRequest(Failure<IReadOnlyList<NotificationPreferenceDto>>(
                ValidationMessages.NotificationPreferencesRequired,
                BusinessCode.ValidationError));
        }

        var targets = new List<NotificationPreferenceTarget>(request.Preferences.Count);

        foreach (var item in request.Preferences)
        {
            if (!TryParseSeverity(item.Severity, out var severity))
            {
                return BadRequest(Failure<IReadOnlyList<NotificationPreferenceDto>>(
                    ValidationMessages.NotificationSeverityInvalid,
                    BusinessCode.ValidationError));
            }

            if (!TryParseChannel(item.Channel, out var channel))
            {
                return BadRequest(Failure<IReadOnlyList<NotificationPreferenceDto>>(
                    ValidationMessages.NotificationChannelInvalid,
                    BusinessCode.ValidationError));
            }

            targets.Add(new NotificationPreferenceTarget(severity, channel, item.Enabled));
        }

        if (targets.Select(target => (target.Severity, target.Channel)).Distinct().Count()
            != targets.Count)
        {
            return BadRequest(Failure<IReadOnlyList<NotificationPreferenceDto>>(
                ValidationMessages.NotificationPreferenceDuplicate,
                BusinessCode.ValidationError));
        }

        var updated = await Mediator.Send(
            new UpdateNotificationPreferencesCommand(CurrentTenant.TenantId, userId, targets));

        return Ok(Success(updated));
    }

    private static bool TryParseSeverity(string? value, out Severity severity) =>
        Enum.TryParse(value, ignoreCase: true, out severity) && Enum.IsDefined(severity);

    private static bool TryParseChannel(string? value, out NotificationChannel channel) =>
        Enum.TryParse(value, ignoreCase: true, out channel) && Enum.IsDefined(channel);
}
