using MediatR;
using OpsPilot.Contracts.Users;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery(
    string? Search,
    string? Role,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResponse<UserDto>>;
