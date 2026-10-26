using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Commands.UpdatePostmortem;

/// <summary>
/// Replaces the written body of the incident's draft postmortem.
/// </summary>
public sealed record UpdatePostmortemCommand(
    Guid IncidentId,
    string Title,
    string Summary,
    string? RootCause,
    string? ActionItems) : IRequest<PostmortemDto>;
