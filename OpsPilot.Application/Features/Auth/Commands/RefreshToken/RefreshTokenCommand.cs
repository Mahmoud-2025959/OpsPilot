using MediatR;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResponseDto>;
