using OpsPilot.Application.Features.Auth.Commands.Login;
using OpsPilot.Application.Features.Auth.Commands.RegisterTenant;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Abstractions;

public interface IAuthRepository
{
    Task<AuthResponseDto> LoginAsync(LoginCommand request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RegisterTenantAsync(RegisterTenantCommand request, CancellationToken cancellationToken = default);
}
