using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Services.Commands.CreateService;

public sealed class CreateServiceCommandHandler : IRequestHandler<CreateServiceCommand, Guid>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ITeamRepository _teamRepository;

    public CreateServiceCommandHandler(
        IServiceRepository serviceRepository,
        ITeamRepository teamRepository)
    {
        _serviceRepository = serviceRepository;
        _teamRepository = teamRepository;
    }

    public async Task<Guid> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        // The identity triple (name, environment) is unique per tenant in the database. Checking
        // first turns the would-be constraint violation into a readable conflict.
        if (await _serviceRepository.NameExistsAsync(request.Name, request.Environment, null, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.ServiceAlreadyExists);
        }

        await ServiceOwner.EnsureTeamExistsAsync(_teamRepository, request.OwnerTeamId, cancellationToken);

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Slug.FromName(request.Name, "service")
            : Slug.From(request.Slug);

        var service = Service.Create(
            request.TenantId,
            request.Name,
            slug,
            request.Environment,
            request.Description,
            request.RepositoryUrl,
            request.OwnerTeamId);

        return await _serviceRepository.AddAsync(service, cancellationToken);
    }
}
