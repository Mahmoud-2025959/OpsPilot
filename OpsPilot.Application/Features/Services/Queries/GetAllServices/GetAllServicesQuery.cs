using MediatR;
using OpsPilot.Contracts.Services;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.Application.Features.Services.Queries.GetAllServices;

public sealed record GetAllServicesQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResponse<ServiceDto>>;
