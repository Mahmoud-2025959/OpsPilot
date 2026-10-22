using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Roles;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly RoleGrantGuard _grantGuard;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        RoleGrantGuard grantGuard)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _grantGuard = grantGuard;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.EmailExistsAsync(request.TenantId, request.Email, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.EmailAlreadyExists);
        }

        var user = User.Create(request.TenantId, request.Email, request.DisplayName);
        user.SetPasswordHash(_passwordHasher.Hash(request.Password));

        await AssignRolesAsync(user, request.Roles, cancellationToken);

        return await _userRepository.AddAsync(user, cancellationToken);
    }

    private async Task AssignRolesAsync(
        User user,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken)
    {
        if (roles is null || roles.Count == 0)
        {
            return;
        }

        var distinctRoles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var found = await _userRepository.GetRolesByNamesAsync(user.TenantId, distinctRoles, cancellationToken);
        var foundNames = found.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = distinctRoles.Where(r => !foundNames.Contains(r)).ToList();
        if (missing.Count != 0)
        {
            throw new DomainException(ExceptionMessages.RolesDoNotExist(missing));
        }

        // ERD §3: creating an account is another way to hand out the Owner role, so it is
        // checked like any other assignment.
        await _grantGuard.EnsureRolesAssignableAsync(user.TenantId, found, cancellationToken);

        foreach (var role in found)
        {
            user.AssignRole(role);
        }
    }
}
