using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Postmortems.Commands.ReturnPostmortemToDraft;

public sealed class ReturnPostmortemToDraftCommandHandler
    : IRequestHandler<ReturnPostmortemToDraftCommand, PostmortemDto>
{
    private readonly IPostmortemRepository _postmortems;
    private readonly IAuditLogWriter _audit;

    public ReturnPostmortemToDraftCommandHandler(
        IPostmortemRepository postmortems,
        IAuditLogWriter audit)
    {
        _postmortems = postmortems;
        _audit = audit;
    }

    public async Task<PostmortemDto> Handle(
        ReturnPostmortemToDraftCommand request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _postmortems.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Postmortem), request.Id);

        var from = postmortem.Status;

        postmortem.ReturnToDraft();

        await _postmortems.UpdateAsync(postmortem, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.PostmortemStatusChanged,
                nameof(Postmortem),
                postmortem.Id.ToString(),
                OldValues: new { status = from.ToString() },
                NewValues: new { status = PostmortemStatus.Draft.ToString() }),
            cancellationToken);

        return postmortem.ToDto();
    }
}
