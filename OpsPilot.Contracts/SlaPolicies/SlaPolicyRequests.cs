namespace OpsPilot.Contracts.SlaPolicies;

/// <summary>
/// Replaces the targets for one severity. The severity itself is the key, which is why the route
/// carries no id: a company keeps exactly one policy per severity (ERD §3).
/// </summary>
public sealed record UpdateSlaPolicyRequest(
    string Severity,
    int ResponseMinutes,
    int ResolutionMinutes);

/// <summary>
/// A batch of policy changes, applied together. Sending only the severities a caller changed is
/// fine — the others are left as they are.
/// </summary>
/// <param name="Policies">The policies to apply. At least one is required.</param>
public sealed record UpdateSlaPoliciesRequest(IReadOnlyList<UpdateSlaPolicyRequest>? Policies = null);
