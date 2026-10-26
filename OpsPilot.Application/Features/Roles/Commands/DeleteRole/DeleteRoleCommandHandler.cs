using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Roles;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditLogWriter _audit;

    public DeleteRoleCommandHandler(IRoleRepository roleRepository, IAuditLogWriter audit)
    {
        _roleRepository = roleRepository;
        _audit = audit;
    }

    public async Task<RoleDto> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Role", request.Id);

        // The five system roles are what every account is provisioned with; retiring one would
        // leave the company unable to run its own permission model.
        if (role.IsSystem)
        {
            throw new ForbiddenException(ExceptionMessages.SystemRoleCannotBeModified(role.Name ?? string.Empty));
        }

        // ERD §3: a role that is still assigned to users must not be deleted; they must be
        // moved to a different role first.
        if (await _roleRepository.HasUsersAsync(role.Id, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.RoleStillAssigned);
        }

        await _roleRepository.DeleteAsync(role, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.RoleChanged,
                nameof(Role),
                role.Id.ToString(),
                OldValues: new { name = role.Name, description = role.Description, isDeleted = false },
                NewValues: new { name = role.Name, description = role.Description, isDeleted = true }),
            cancellationToken);

        return role.ToDto();
    }
}
