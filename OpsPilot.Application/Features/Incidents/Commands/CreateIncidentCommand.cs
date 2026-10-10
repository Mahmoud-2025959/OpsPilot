using MediatR;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Incidents.Commands;

public sealed record CreateIncidentCommand(
    Guid TenantId,
    Guid ServiceId,
    string Title,
    string Description,
    Severity Severity,
    Guid CreatedBy,
    string? CorrelationKey = null) : IRequest<Guid>;