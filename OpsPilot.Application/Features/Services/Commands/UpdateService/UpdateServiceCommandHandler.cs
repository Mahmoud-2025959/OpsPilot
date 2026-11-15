using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Services;
using OpsPilot.Domain.Common;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Services.Commands.UpdateService;

public sealed class UpdateServiceCommandHandler : IRequestHandler<UpdateServiceCommand, ServiceDto>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ITeamRepository _teamRepository;

    public UpdateServiceCommandHandler(
        IServiceRepository serviceRepository,
        ITeamRepository teamRepository)
    {
        _serviceRepository = serviceRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ServiceDto> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Service", request.Id);

        // Moving onto a name already taken in the target environment is a conflict with the
        // existing service, not a malformed request. Excluding this row lets a no-op edit through.
        if (await _serviceRepository.NameExistsAsync(
                request.Name,
                request.Environment,
                request.Id,
                cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.ServiceAlreadyExists);
        }

        await ServiceOwner.EnsureTeamExistsAsync(_teamRepository, request.OwnerTeamId, cancellationToken);

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Slug.FromName(request.Name, "service")
            : Slug.From(request.Slug);

        // PUT replaces the whole representation, so an omitted owner team clears it.
        service.UpdateDetails(request.Name, slug, request.Environment, request.Description);
        service.ChangeRepository(request.RepositoryUrl);
        service.AssignOwnerTeam(request.OwnerTeamId);

        await _serviceRepository.UpdateAsync(service, cancellationToken);

        return service.ToDto();
    }
}
