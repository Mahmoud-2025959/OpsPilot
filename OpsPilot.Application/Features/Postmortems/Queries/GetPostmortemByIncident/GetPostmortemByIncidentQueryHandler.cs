using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Queries.GetPostmortemByIncident;

public sealed class GetPostmortemByIncidentQueryHandler
    : IRequestHandler<GetPostmortemByIncidentQuery, PostmortemDto?>
{
    private readonly IPostmortemRepository _repository;

    public GetPostmortemByIncidentQueryHandler(IPostmortemRepository repository) =>
        _repository = repository;

    public async Task<PostmortemDto?> Handle(
        GetPostmortemByIncidentQuery request,
        CancellationToken cancellationToken)
    {
        var postmortem = await _repository.GetByIncidentAsync(request.IncidentId, cancellationToken);

        return postmortem?.ToDto();
    }
}
