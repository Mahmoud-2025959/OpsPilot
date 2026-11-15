using MediatR;
using OpsPilot.Contracts.SlaPolicies;

namespace OpsPilot.Application.Features.SlaPolicies.Queries.GetSlaPolicies;

/// <summary>
/// The company's four SLA policies. The set is small and fixed — one row per severity — so it is
/// returned whole rather than paged.
/// </summary>
public sealed record GetSlaPoliciesQuery : IRequest<IReadOnlyList<SlaPolicyDto>>;
