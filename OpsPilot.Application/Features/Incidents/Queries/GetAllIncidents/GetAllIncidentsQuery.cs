using MediatR;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Incidents.Queries.GetAllIncidents;

/// <summary>
/// Lists incidents for the ambient tenant. The tenant is never supplied by the caller; the
/// global query filter guarantees it.
/// </summary>
public sealed record GetAllIncidentsQuery(
    string? Status,
    string? Severity,
    Guid? ServiceId,
    Guid? AssignedTo,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<IncidentDto>>;
