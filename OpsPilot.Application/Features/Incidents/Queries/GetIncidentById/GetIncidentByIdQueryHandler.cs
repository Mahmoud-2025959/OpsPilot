using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Incidents;

namespace OpsPilot.Application.Features.Incidents.Queries.GetIncidentById;

public sealed class GetIncidentByIdQueryHandler : IRequestHandler<GetIncidentByIdQuery, IncidentDto?>
{
    private readonly IIncidentRepository _repository;

    public GetIncidentByIdQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentDto?> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return incident?.ToDto();
    }
}
