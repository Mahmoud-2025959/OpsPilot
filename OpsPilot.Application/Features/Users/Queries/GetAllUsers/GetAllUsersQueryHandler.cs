using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Users.Queries.GetAllUsers;

public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PagedResponse<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResponse<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _userRepository.GetPagedAsync(
            request.Search,
            request.Role,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(u => u.ToDto()).ToList();

        return PagedResponse<UserDto>.Create(dtos, page, pageSize, totalCount);
    }
}
