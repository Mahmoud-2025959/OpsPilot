using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Commands.SubmitPostmortemForReview;

/// <summary>Sends a draft retrospective for review (Draft → InReview).</summary>
public sealed record SubmitPostmortemForReviewCommand(Guid Id) : IRequest<PostmortemDto>;
