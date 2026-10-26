using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.SlaPolicies.Commands.UpdateSlaPolicies;
using OpsPilot.Application.Features.SlaPolicies.Queries.GetSlaPolicies;
using OpsPilot.Contracts.SlaPolicies;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// Reads and retunes the company's SLA targets (plan, UC-05 and FR-14).
/// </summary>
/// <remarks>
/// The set is fixed at one policy per severity, so the route carries no id: the severity in the
/// body is the key. Both verbs require <c>sla.manage</c>, which only Owner and Admin hold.
/// </remarks>
[Route("api/v1/sla-policies")]
public sealed class SlaPoliciesController : BaseController
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.SlaManage)]
    public async Task<ActionResult<Response<IReadOnlyList<SlaPolicyDto>>>> GetAll()
    {
        var result = await Mediator.Send(new GetSlaPoliciesQuery());
        return Ok(Success(result));
    }

    [HttpPut]
    [Authorize(Policy = PermissionNames.SlaManage)]
    public async Task<ActionResult<Response<IReadOnlyList<SlaPolicyDto>>>> Update(
        [FromBody] UpdateSlaPoliciesRequest request)
    {
        if (request.Policies is null || request.Policies.Count == 0)
        {
            return BadRequest(Failure<IReadOnlyList<SlaPolicyDto>>(
                ValidationMessages.SlaPoliciesRequired,
                BusinessCode.ValidationError));
        }

        var targets = new List<SlaPolicyTarget>(request.Policies.Count);

        foreach (var item in request.Policies)
        {
            if (!TryParseSeverity(item.Severity, out var severity))
            {
                return BadRequest(Failure<IReadOnlyList<SlaPolicyDto>>(
                    ValidationMessages.SlaSeverityInvalid,
                    BusinessCode.ValidationError));
            }

            targets.Add(new SlaPolicyTarget(severity, item.ResponseMinutes, item.ResolutionMinutes));
        }

        var updated = await Mediator.Send(
            new UpdateSlaPoliciesCommand(CurrentTenant.TenantId, targets));

        return Ok(Success(updated));
    }

    private static bool TryParseSeverity(string? value, out Severity severity) =>
        Enum.TryParse(value, ignoreCase: true, out severity) && Enum.IsDefined(severity);
}
