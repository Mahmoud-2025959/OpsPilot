using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemById;

/// <summary>Reads one retrospective by its own id.</summary>
public sealed record GetPostmortemByIdQuery(Guid Id) : IRequest<PostmortemDto?>;
