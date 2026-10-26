using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.RegisterTenant;

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, AuthResponseDto>
{
    private readonly IAuthRepository _authRepository;

    public RegisterTenantCommandHandler(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public Task<AuthResponseDto> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        return _authRepository.RegisterTenantAsync(request, cancellationToken);
    }
}
