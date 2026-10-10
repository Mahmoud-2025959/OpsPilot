using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Incidents.Commands.AddIncidentComment;

public sealed class AddIncidentCommentCommandHandler : IRequestHandler<AddIncidentCommentCommand>
{
    private readonly IIncidentRepository _repository;

    public AddIncidentCommentCommandHandler(IIncidentRepository repository) => _repository = repository;

    public async Task Handle(AddIncidentCommentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        var now = DateTimeOffset.UtcNow;
        var comment = IncidentComment.Create(incident.TenantId, incident.Id, request.AuthorId, request.Body, now);

        var evt = IncidentEvent.Create(
            incident.TenantId,
            incident.Id,
            IncidentEventType.CommentAdded,
            occurredAt: now,
            payload: request.AuthorId.ToString());

        await _repository.AddCommentAsync(comment, cancellationToken);
        await _repository.AddEventAsync(evt, cancellationToken);
    }
}
