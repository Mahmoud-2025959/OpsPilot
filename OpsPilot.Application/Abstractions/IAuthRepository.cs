using OpsPilot.Application.Features.Auth.Commands.Login;
using OpsPilot.Application.Features.Auth.Commands.Register;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Abstractions;

public interface IAuthRepository
{
    Task<AuthResponseDto> LoginAsync(LoginCommand request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RegisterAsync(RegisterCommand request, CancellationToken cancellationToken = default);
}
