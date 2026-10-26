using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Commands.ReturnPostmortemToDraft;

/// <summary>
/// Rejects the review and sends the retrospective back to its author (InReview → Draft).
/// </summary>
public sealed record ReturnPostmortemToDraftCommand(Guid Id) : IRequest<PostmortemDto>;
