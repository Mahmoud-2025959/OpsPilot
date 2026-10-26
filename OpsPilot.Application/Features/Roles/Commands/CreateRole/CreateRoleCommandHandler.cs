using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Roles;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly RoleGrantGuard _grantGuard;
    private readonly IAuditLogWriter _audit;

    public CreateRoleCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        RoleGrantGuard grantGuard,
        IAuditLogWriter audit)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _grantGuard = grantGuard;
        _audit = audit;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        // The five system roles are provisioned with every company, so their names are already
        // taken. Saying so is clearer than letting the unique index report a clash.
        if (SystemRoleNames.All.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new ConflictException(ExceptionMessages.RoleNameReserved(name));
        }

        if (await _roleRepository.NameExistsAsync(name, null, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.RoleAlreadyExists);
        }

        var permissions = await RolePermissionResolver.ResolveAsync(
            _permissionRepository,
            request.Permissions,
            cancellationToken);

        await _grantGuard.EnsureGrantableAsync(
            request.TenantId,
            permissions.Select(p => p.Name).ToList(),
            cancellationToken);

        var role = Role.Create(request.TenantId, name, request.Description);

        foreach (var permission in permissions)
        {
            role.AssignPermission(permission);
        }

        var id = await _roleRepository.AddAsync(role, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.RoleChanged,
                nameof(Role),
                id.ToString(),
                NewValues: new { role.Name, role.Description }),
            cancellationToken);

        return role.ToDto();
    }
}
