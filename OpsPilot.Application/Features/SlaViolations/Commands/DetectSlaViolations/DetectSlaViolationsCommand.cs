using MediatR;

namespace OpsPilot.Application.Features.SlaViolations.Commands.DetectSlaViolations;

/// <summary>
/// One pass of the SLA breach sweep for the ambient tenant (plan, UC-12).
/// </summary>
/// <remarks>
/// The moment the sweep judges against arrives on the command instead of being read from the
/// clock inside the handler, so whoever calls it owns "now": the worker passes the real time,
/// and a test can place an incident on either side of its deadline without waiting for one to
/// pass. It returns how many breaches it recorded.
/// </remarks>
public sealed record DetectSlaViolationsCommand(DateTimeOffset Now) : IRequest<int>;
