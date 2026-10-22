using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Services;

namespace OpsPilot.Application.Features.Services.Queries.GetServiceById;

public sealed class GetServiceByIdQueryHandler : IRequestHandler<GetServiceByIdQuery, ServiceDto?>
{
    private readonly IServiceRepository _repository;

    public GetServiceByIdQueryHandler(IServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ServiceDto?> Handle(
        GetServiceByIdQuery request,
        CancellationToken cancellationToken)
    {
        var service = await _repository.GetByIdAsync(request.Id, cancellationToken);

        return service?.ToDto();
    }
}
