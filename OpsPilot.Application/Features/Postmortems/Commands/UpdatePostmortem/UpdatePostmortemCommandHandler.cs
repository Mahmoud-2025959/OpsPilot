using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Postmortems.Commands.UpdatePostmortem;

public sealed class UpdatePostmortemCommandHandler
    : IRequestHandler<UpdatePostmortemCommand, PostmortemDto>
{
    private readonly IPostmortemRepository _postmortems;
    private readonly IAuditLogWriter _audit;

    public UpdatePostmortemCommandHandler(IPostmortemRepository postmortems, IAuditLogWriter audit)
    {
        _postmortems = postmortems;
        _audit = audit;
    }

    public async Task<PostmortemDto> Handle(
        UpdatePostmortemCommand request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _postmortems.GetByIncidentAsync(request.IncidentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Postmortem), request.IncidentId);

        // The aggregate refuses this outside Draft; the exception surfaces as a 409.
        postmortem.UpdateContent(
            request.Title,
            request.Summary,
            request.RootCause,
            request.ActionItems);

        await _postmortems.UpdateAsync(postmortem, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.PostmortemUpdated,
                nameof(Postmortem),
                postmortem.Id.ToString(),
                NewValues: new { postmortem.Title }),
            cancellationToken);

        return postmortem.ToDto();
    }
}
