using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentComments;

public sealed class GetIncidentCommentsQueryHandler
    : IRequestHandler<GetIncidentCommentsQuery, IReadOnlyList<IncidentCommentDto>>
{
    private readonly IIncidentRepository _repository;

    public GetIncidentCommentsQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<IncidentCommentDto>> Handle(
        GetIncidentCommentsQuery request,
        CancellationToken cancellationToken)
    {
        var comments = await _repository.GetCommentsAsync(request.IncidentId, cancellationToken);
        return comments.Select(c => c.ToDto()).ToList();
    }
}
