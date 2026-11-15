using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Roles;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditLogWriter _audit;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository, IAuditLogWriter audit)
    {
        _roleRepository = roleRepository;
        _audit = audit;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("Role", request.Id);

        // System roles are part of the company's shape: they are what every account is
        // provisioned with, so their names may not drift.
        if (role.IsSystem)
        {
            throw new ForbiddenException(ExceptionMessages.SystemRoleCannotBeModified(role.Name ?? string.Empty));
        }

        var name = request.Name.Trim();

        if (await _roleRepository.NameExistsAsync(name, role.Id, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.RoleAlreadyExists);
        }

        var previousName = role.Name;
        var previousDescription = role.Description;

        role.Rename(name, request.Description);

        await _roleRepository.UpdateAsync(role, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.RoleChanged,
                nameof(Role),
                role.Id.ToString(),
                OldValues: new { name = previousName, description = previousDescription },
                NewValues: new { name = role.Name, description = role.Description }),
            cancellationToken);

        return role.ToDto();
    }
}
