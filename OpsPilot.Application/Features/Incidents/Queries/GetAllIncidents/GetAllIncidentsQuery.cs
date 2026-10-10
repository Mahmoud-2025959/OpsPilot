using MediatR;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Incidents.Queries.GetAllIncidents;

public sealed record GetAllIncidentsQuery(
    Guid? TenantId,
    string? Status,
    string? Severity,
    Guid? ServiceId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<IncidentDto>>;
