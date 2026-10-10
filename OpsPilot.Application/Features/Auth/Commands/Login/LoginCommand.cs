using MediatR;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Identifier, string Password) : IRequest<AuthResponseDto>;
