using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemByIncident;

/// <summary>Reads the incident's retrospective, or null when none has been opened.</summary>
public sealed record GetPostmortemByIncidentQuery(Guid IncidentId) : IRequest<PostmortemDto?>;
