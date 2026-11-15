using OpsPilot.Contracts.SlaPolicies;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class SlaPolicyMappings
{
    public static SlaPolicyDto ToDto(this SlaPolicy policy) =>
        new(policy.Id,
            policy.Severity.ToString(),
            policy.ResponseMinutes,
            policy.ResolutionMinutes,
            policy.UpdatedAt);
}
