using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Queries.GetUserPermissions;

public sealed class GetUserPermissionsQueryHandler
    : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserPermissionsQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserPermissionsDto?> Handle(
        GetUserPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        // The repository loads the roles and their permissions, which is exactly what the
        // mapping needs; the tenant filter keeps other companies out of reach.
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var dto = user.ToDto();

        return new UserPermissionsDto(user.Id, dto.Roles, dto.Permissions);
    }
}
