using MediatR;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentById;

public sealed record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDto?>;
