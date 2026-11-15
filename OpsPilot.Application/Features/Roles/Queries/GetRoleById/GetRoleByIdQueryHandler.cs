using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Roles;

namespace OpsPilot.Application.Features.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IRoleRepository _roleRepository;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);

        return role?.ToDto();
    }
}
