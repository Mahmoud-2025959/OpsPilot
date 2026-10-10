using MediatR;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentComments;

public sealed record GetIncidentCommentsQuery(Guid IncidentId) : IRequest<IReadOnlyList<IncidentCommentDto>>;
