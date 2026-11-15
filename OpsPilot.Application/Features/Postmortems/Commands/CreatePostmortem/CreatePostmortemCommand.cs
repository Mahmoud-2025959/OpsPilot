using MediatR;
using OpsPilot.Contracts.Postmortems;

namespace OpsPilot.Application.Features.Postmortems.Commands.CreatePostmortem;

/// <summary>
/// Opens the retrospective for an incident. The company and the author come from the request
/// context, never from a body, so a caller cannot write into another company's incident.
/// </summary>
public sealed record CreatePostmortemCommand(
    Guid TenantId,
    Guid IncidentId,
    Guid AuthorId,
    string Title,
    string Summary,
    string? RootCause,
    string? ActionItems) : IRequest<PostmortemDto>;
