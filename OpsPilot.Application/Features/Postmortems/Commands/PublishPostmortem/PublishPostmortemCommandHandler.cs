using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Postmortems.Commands.PublishPostmortem;

public sealed class PublishPostmortemCommandHandler
    : IRequestHandler<PublishPostmortemCommand, PostmortemDto>
{
    private readonly IPostmortemRepository _postmortems;
    private readonly IIncidentRepository _incidents;
    private readonly IAuditLogWriter _audit;

    public PublishPostmortemCommandHandler(
        IPostmortemRepository postmortems,
        IIncidentRepository incidents,
        IAuditLogWriter audit)
    {
        _postmortems = postmortems;
        _incidents = incidents;
        _audit = audit;
    }

    public async Task<PostmortemDto> Handle(
        PublishPostmortemCommand request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _postmortems.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Postmortem), request.Id);

        var incident = await _incidents.GetByIdAsync(postmortem.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), postmortem.IncidentId);

        // A retrospective is only final once the incident itself is done. Publishing one for an
        // incident that is still being worked would freeze a story that is not finished yet.
        if (incident.Status is not (IncidentStatus.Resolved or IncidentStatus.Closed))
        {
            throw new ConflictException(ExceptionMessages.PostmortemRequiresCompletedIncident);
        }

        var from = postmortem.Status;

        postmortem.Publish(DateTimeOffset.UtcNow);

        await _postmortems.UpdateAsync(postmortem, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.PostmortemStatusChanged,
                nameof(Postmortem),
                postmortem.Id.ToString(),
                OldValues: new { status = from.ToString() },
                NewValues: new { status = PostmortemStatus.Published.ToString() }),
            cancellationToken);

        return postmortem.ToDto();
    }
}
