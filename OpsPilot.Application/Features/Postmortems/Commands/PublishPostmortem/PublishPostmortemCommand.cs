using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Commands.PublishPostmortem;

/// <summary>Publishes a reviewed retrospective (InReview → Published).</summary>
public sealed record PublishPostmortemCommand(Guid Id) : IRequest<PostmortemDto>;
