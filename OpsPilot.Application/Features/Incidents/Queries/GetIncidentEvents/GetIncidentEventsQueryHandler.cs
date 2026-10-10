using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentEvents;

public sealed class GetIncidentEventsQueryHandler
    : IRequestHandler<GetIncidentEventsQuery, IReadOnlyList<IncidentEventDto>>
{
    private readonly IIncidentRepository _repository;

    public GetIncidentEventsQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<IncidentEventDto>> Handle(
        GetIncidentEventsQuery request,
        CancellationToken cancellationToken)
    {
        var events = await _repository.GetEventsAsync(request.IncidentId, cancellationToken);
        return events.Select(e => e.ToDto()).ToList();
    }
}
