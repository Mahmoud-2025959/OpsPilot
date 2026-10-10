using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IAuthRepository _authRepository;

    public LoginCommandHandler(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        return _authRepository.LoginAsync(request, cancellationToken);
    }
}
