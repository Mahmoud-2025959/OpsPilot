using MediatR;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetAllPostmortems;

/// <summary>
/// Pages the company's retrospectives, newest first, optionally narrowed by state or incident.
/// </summary>
public sealed record GetAllPostmortemsQuery(
    string? Status,
    Guid? IncidentId,
    int Page,
    int PageSize) : IRequest<PagedResponse<PostmortemDto>>;
