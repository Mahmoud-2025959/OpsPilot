using MediatR;

namespace OpsPilot.Application.Features.Incidents.Commands.AddIncidentComment;

public sealed record AddIncidentCommentCommand(Guid IncidentId, Guid AuthorId, string Body) : IRequest;
