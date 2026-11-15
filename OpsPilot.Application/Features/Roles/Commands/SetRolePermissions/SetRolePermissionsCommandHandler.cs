using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Roles;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandHandler : IRequestHandler<SetRolePermissionsCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly RoleGrantGuard _grantGuard;
    private readonly IAuditLogWriter _audit;

    public SetRolePermissionsCommandHandler(
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

    public async Task<RoleDto> Handle(SetRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Role", request.Id);

        // The Owner role is the company's last line of control: its permission set is frozen
        // so that nobody, however privileged, can lock the company out of its own settings.
        if (role.IsOwner)
        {
            throw new ForbiddenException(ExceptionMessages.OwnerPermissionsFrozen);
        }

        var permissions = await RolePermissionResolver.ResolveAsync(
            _permissionRepository,
            request.Permissions,
            cancellationToken);

        await _grantGuard.EnsureGrantableAsync(
            request.TenantId,
            permissions.Select(p => p.Name).ToList(),
            cancellationToken);

        var previous = role.Permissions.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        // The list is authoritative: drop what is no longer wanted before adding what is.
        foreach (var held in role.Permissions.ToList())
        {
            if (!permissions.Any(p => p.Id == held.Id))
            {
                role.RemovePermission(held);
            }
        }

        foreach (var permission in permissions)
        {
            role.AssignPermission(permission);
        }

        await _roleRepository.UpdateAsync(role, cancellationToken);

        var current = role.Permissions.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.RolePermissionsChanged,
                nameof(Role),
                role.Id.ToString(),
                OldValues: new { permissions = previous },
                NewValues: new { permissions = current }),
            cancellationToken);

        return role.ToDto();
    }
}
