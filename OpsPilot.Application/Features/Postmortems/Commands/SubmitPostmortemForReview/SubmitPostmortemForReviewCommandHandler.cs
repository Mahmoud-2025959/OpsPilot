using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Postmortems.Commands.SubmitPostmortemForReview;

public sealed class SubmitPostmortemForReviewCommandHandler
    : IRequestHandler<SubmitPostmortemForReviewCommand, PostmortemDto>
{
    private readonly IPostmortemRepository _postmortems;
    private readonly IAuditLogWriter _audit;

    public SubmitPostmortemForReviewCommandHandler(
        IPostmortemRepository postmortems,
        IAuditLogWriter audit)
    {
        _postmortems = postmortems;
        _audit = audit;
    }

    public async Task<PostmortemDto> Handle(
        SubmitPostmortemForReviewCommand request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _postmortems.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Postmortem), request.Id);

        var from = postmortem.Status;

        postmortem.MoveToReview();

        await _postmortems.UpdateAsync(postmortem, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.PostmortemStatusChanged,
                nameof(Postmortem),
                postmortem.Id.ToString(),
                OldValues: new { status = from.ToString() },
                NewValues: new { status = PostmortemStatus.InReview.ToString() }),
            cancellationToken);

        return postmortem.ToDto();
    }
}
