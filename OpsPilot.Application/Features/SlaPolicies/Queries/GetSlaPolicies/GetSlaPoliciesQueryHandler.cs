using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.SlaPolicies;

namespace OpsPilot.Application.Features.SlaPolicies.Queries.GetSlaPolicies;

public sealed class GetSlaPoliciesQueryHandler
    : IRequestHandler<GetSlaPoliciesQuery, IReadOnlyList<SlaPolicyDto>>
{
    private readonly ISlaPolicyRepository _repository;

    public GetSlaPoliciesQueryHandler(ISlaPolicyRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<SlaPolicyDto>> Handle(
        GetSlaPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        // The repository orders by severity, which is the order the settings screen shows them in.
        var policies = await _repository.GetAllAsync(cancellationToken);

        return policies.Select(policy => policy.ToDto()).ToList();
    }
}
