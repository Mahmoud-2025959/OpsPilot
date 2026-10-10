using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Incidents;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Incidents.Queries.GetAllIncidents;

public sealed class GetAllIncidentsQueryHandler
    : IRequestHandler<GetAllIncidentsQuery, PagedResponse<IncidentDto>>
{
    private readonly IIncidentRepository _repository;

    public GetAllIncidentsQueryHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<IncidentDto>> Handle(
        GetAllIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        IncidentStatus? status = Enum.TryParse<IncidentStatus>(request.Status, ignoreCase: true, out var s) ? s : null;
        Severity? severity = Enum.TryParse<Severity>(request.Severity, ignoreCase: true, out var sv) ? sv : null;

        var (items, totalCount) = await _repository.GetPagedAsync(
            request.TenantId,
            status,
            severity,
            request.ServiceId,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(i => i.ToDto()).ToList();

        return PagedResponse<IncidentDto>.Create(dtos, page, pageSize, totalCount);
    }
}
