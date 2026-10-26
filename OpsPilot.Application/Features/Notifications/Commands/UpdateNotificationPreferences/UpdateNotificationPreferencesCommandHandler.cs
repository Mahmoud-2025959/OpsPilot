using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Notifications;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

public sealed class UpdateNotificationPreferencesCommandHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand, IReadOnlyList<NotificationPreferenceDto>>
{
    private readonly INotificationRepository _repository;

    public UpdateNotificationPreferencesCommandHandler(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var rows = await _repository.GetPreferencesAsync(request.UserId, cancellationToken);
        var byKey = rows.ToDictionary(row => (row.Severity, row.Channel));

        var added = new List<NotificationPreference>(request.Targets.Count);

        foreach (var target in request.Targets)
        {
            if (byKey.TryGetValue((target.Severity, target.Channel), out var row))
            {
                // Loaded rows are tracked, so flipping the flag is enough — the batch save
                // below persists it along with everything else.
                if (target.Enabled)
                {
                    row.Enable();
                }
                else
                {
                    row.Disable();
                }
            }
            else
            {
                var created = NotificationPreference.Create(
                    request.TenantId,
                    request.UserId,
                    target.Channel,
                    target.Severity);

                if (!target.Enabled)
                {
                    created.Disable();
                }

                added.Add(created);
            }
        }

        // One save for the batch, so a rejected row cannot leave half the changes applied.
        await _repository.SavePreferencesAsync(added, cancellationToken);

        // The whole effective set comes back — every severity and channel, not only the stored
        // rows — because a combination with no row is enabled by default and a client rendering
        // its switches needs that answer spelled out rather than implied by an absence.
        var current = new Dictionary<(Severity, NotificationChannel), bool>();

        foreach (var row in rows)
        {
            current[(row.Severity, row.Channel)] = row.IsEnabled;
        }

        foreach (var row in added)
        {
            current[(row.Severity, row.Channel)] = row.IsEnabled;
        }

        var full = new List<NotificationPreferenceDto>(8);

        // Enum members are stored as strings, so SQL would order these alphabetically; the
        // numeric order (Critical, High, Medium, Low) only survives if it is applied here.
        foreach (var severity in Enum.GetValues<Severity>().OrderBy(severity => severity))
        {
            foreach (var channel in Enum.GetValues<NotificationChannel>().OrderBy(channel => channel))
            {
                full.Add(new NotificationPreferenceDto(
                    severity.ToString(),
                    channel.ToString(),
                    !current.TryGetValue((severity, channel), out var enabled) || enabled));
            }
        }

        return full;
    }
}
