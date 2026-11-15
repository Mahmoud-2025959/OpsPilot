using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;

namespace OpsPilot.Application.Features.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user?.ToCurrentUserDto();
    }
}
