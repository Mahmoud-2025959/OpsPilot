using MediatR;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentEvents;

public sealed record GetIncidentEventsQuery(Guid IncidentId) : IRequest<IReadOnlyList<IncidentEventDto>>;
