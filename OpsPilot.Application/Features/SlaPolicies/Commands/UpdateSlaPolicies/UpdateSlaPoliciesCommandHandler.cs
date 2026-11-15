using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.SlaPolicies;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.SlaPolicies.Commands.UpdateSlaPolicies;

public sealed class UpdateSlaPoliciesCommandHandler
    : IRequestHandler<UpdateSlaPoliciesCommand, IReadOnlyList<SlaPolicyDto>>
{
    private readonly ISlaPolicyRepository _repository;

    public UpdateSlaPoliciesCommandHandler(ISlaPolicyRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<SlaPolicyDto>> Handle(
        UpdateSlaPoliciesCommand request,
        CancellationToken cancellationToken)
    {
        var policies = await _repository.GetAllAsync(cancellationToken);
        var bySeverity = policies.ToDictionary(policy => policy.Severity);

        var changed = new List<SlaPolicy>(request.Policies.Count);

        foreach (var target in request.Policies)
        {
            // A severity with no row means the tenant was never fully provisioned. Rather than
            // invent one here, refuse: the missing policy is a data fault worth surfacing.
            if (!bySeverity.TryGetValue(target.Severity, out var policy))
            {
                throw new EntityNotFoundException(ExceptionMessages.SlaPolicyNotFound(target.Severity));
            }

            policy.Retune(target.ResponseMinutes, target.ResolutionMinutes);
            changed.Add(policy);
        }

        // One save for the batch, so a rejected row cannot leave half the changes applied.
        await _repository.UpdateRangeAsync(changed, cancellationToken);

        // The whole set comes back, so the caller sees the effect on the severities it did not
        // touch as well.
        var all = await _repository.GetAllAsync(cancellationToken);

        return all.Select(policy => policy.ToDto()).ToList();
    }
}
