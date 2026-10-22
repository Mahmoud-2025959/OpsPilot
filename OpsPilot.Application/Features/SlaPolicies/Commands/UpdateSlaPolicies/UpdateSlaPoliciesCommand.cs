using MediatR;
using OpsPilot.Contracts.SlaPolicies;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.SlaPolicies.Commands.UpdateSlaPolicies;

/// <summary>One severity's new targets.</summary>
public sealed record SlaPolicyTarget(Severity Severity, int ResponseMinutes, int ResolutionMinutes);

/// <summary>
/// Retunes the company's SLA targets (plan, UC-05 and FR-14). Severities that are not supplied
/// keep their current values, so a caller can save one row or all four.
/// </summary>
public sealed record UpdateSlaPoliciesCommand(
    Guid TenantId,
    IReadOnlyList<SlaPolicyTarget> Policies) : IRequest<IReadOnlyList<SlaPolicyDto>>;
