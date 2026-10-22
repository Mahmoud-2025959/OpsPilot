using MediatR;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Services.Commands.CreateService;

/// <summary>
/// Registers a service in the catalogue. The tenant comes from the caller, never from the body.
/// </summary>
/// <param name="Slug">Optional handle; when null it is derived from <paramref name="Name"/>.</param>
public sealed record CreateServiceCommand(
    Guid TenantId,
    string Name,
    EnvironmentType Environment,
    string? Slug = null,
    string? Description = null,
    string? RepositoryUrl = null,
    Guid? OwnerTeamId = null) : IRequest<Guid>;
