using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly IAuthRepository _authRepository;

    public RegisterCommandHandler(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        return _authRepository.RegisterAsync(request, cancellationToken);
    }
}
