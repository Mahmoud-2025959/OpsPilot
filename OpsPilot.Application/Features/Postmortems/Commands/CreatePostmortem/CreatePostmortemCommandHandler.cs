using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Postmortems.Commands.CreatePostmortem;

public sealed class CreatePostmortemCommandHandler
    : IRequestHandler<CreatePostmortemCommand, PostmortemDto>
{
    private readonly IPostmortemRepository _postmortems;
    private readonly IIncidentRepository _incidents;
    private readonly IAuditLogWriter _audit;

    public CreatePostmortemCommandHandler(
        IPostmortemRepository postmortems,
        IIncidentRepository incidents,
        IAuditLogWriter audit)
    {
        _postmortems = postmortems;
        _incidents = incidents;
        _audit = audit;
    }

    public async Task<PostmortemDto> Handle(
        CreatePostmortemCommand request,
        CancellationToken cancellationToken)
    {
        // The tenant-filtered lookup refuses an incident id that names nothing and one that
        // belongs to another company with the same answer.
        var incident = await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId);

        // Checked before the insert on purpose: the unique index on IncidentId would otherwise
        // surface a duplicate as an unhandled database error instead of a 409.
        if (await _postmortems.GetByIncidentAsync(request.IncidentId, cancellationToken) is not null)
        {
            throw new ConflictException(ExceptionMessages.PostmortemAlreadyExists);
        }

        var postmortem = Postmortem.Create(
            incident.TenantId,
            incident.Id,
            request.Title,
            request.Summary,
            request.AuthorId);

        postmortem.RecordRootCause(request.RootCause);
        postmortem.RecordActionItems(request.ActionItems);

        await _postmortems.AddAsync(postmortem, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.PostmortemCreated,
                nameof(Postmortem),
                postmortem.Id.ToString(),
                NewValues: new { postmortem.IncidentId, postmortem.Title }),
            cancellationToken);

        return postmortem.ToDto();
    }
}
