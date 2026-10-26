using MediatR;
using OpsPilot.Contracts.Services;

namespace OpsPilot.Application.Features.Services.Queries.GetServiceById;

public sealed record GetServiceByIdQuery(Guid Id) : IRequest<ServiceDto?>;
