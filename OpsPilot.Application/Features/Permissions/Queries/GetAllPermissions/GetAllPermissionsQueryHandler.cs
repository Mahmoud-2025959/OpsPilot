using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Permissions;

namespace OpsPilot.Application.Features.Permissions.Queries.GetAllPermissions;

public sealed class GetAllPermissionsQueryHandler
    : IRequestHandler<GetAllPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetAllPermissionsQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(
        GetAllPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.GetAllAsync(cancellationToken);

        return permissions
            .OrderBy(p => p.Category, StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => p.ToDto())
            .ToList();
    }
}
