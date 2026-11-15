using MediatR;
using OpsPilot.Contracts.Services;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Features.Services.Commands.UpdateService;

/// <summary>Edits the mutable fields of an existing service.</summary>
public sealed record UpdateServiceCommand(
    Guid Id,
    string Name,
    EnvironmentType Environment,
    string? Slug = null,
    string? Description = null,
    string? RepositoryUrl = null,
    Guid? OwnerTeamId = null) : IRequest<ServiceDto>;
